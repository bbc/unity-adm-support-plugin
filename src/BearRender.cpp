#include "BearRender.h"
#include "ExceptionHandler.h"
#include <ear/metadata.hpp>
#include <../src/common.h>
#include <fstream>
#include <array>

// TODO: Need a working relative path
#define DEFAULT_TENSORFILE_NAME "default.tf"

namespace {
    BearRender* bearRender = nullptr;

    bool fileReadable(const std::string& name) {
        if(FILE *file = fopen(name.c_str(), "r")) {
            fclose(file);
            return true;
        }
        return false;
    }
}

BearRender* getBearSingleton()
{
    // TODO - need a destroy method to tidy this up when unloading lib
    if(!bearRender) {
        bearRender = new BearRender();
    }
    return bearRender;
}

BearRender::BearRender()
{
}

BearRender::~BearRender()
{
    if(src != nullptr) {
        src_delete(src);
        src = nullptr;
    }
}

bool BearRender::setupBear(std::shared_ptr<AudioExtractor> usingExtractor, size_t maxObjectsChannels, size_t maxDirectSpeakersChannels, size_t maxHoaChannels, size_t maxAnticipatedBlockFrameRequest, size_t rendererInternalBlockFrameCount, std::string dataPath, std::string fft)
{
    assert(usingExtractor);
    audioExtractor = usingExtractor;

    bearConfig.set_sample_rate(audioExtractor->getSampleRate());
    bearConfig.set_period_size(rendererInternalBlockFrameCount);
    bearConfig.set_num_direct_speakers_channels(maxDirectSpeakersChannels);
    bearConfig.set_num_objects_channels(maxObjectsChannels);
    bearConfig.set_num_hoa_channels(maxHoaChannels);
    bearConfig.set_data_path(dataPath.length() == 0 ? DEFAULT_TENSORFILE_NAME : dataPath);
    bearConfig.set_fft_implementation(fft.length() == 0 ? "default" : fft);

    bearOutputBuffers = std::vector<std::shared_ptr<std::vector<float>>>(2, nullptr);
    bearOutputBuffers_RawPointers = std::vector<float*>(2, nullptr);
    bearObjectInputBuffers = std::vector<std::shared_ptr<std::vector<float>>>(maxObjectsChannels, nullptr);
    bearObjectInputBuffers_RawPointers = std::vector<float*>(maxObjectsChannels, nullptr);
    bearDirectSpeakersInputBuffers = std::vector<std::shared_ptr<std::vector<float>>>(maxDirectSpeakersChannels, nullptr);
    bearDirectSpeakersInputBuffers_RawPointers = std::vector<float*>(maxDirectSpeakersChannels, nullptr);
    bearHoaInputBuffers = std::vector<std::shared_ptr<std::vector<float>>>(maxHoaChannels, nullptr);
    bearHoaInputBuffers_RawPointers = std::vector<float*>(maxHoaChannels, nullptr);
    setBufferFrameCounts(maxAnticipatedBlockFrameRequest);

    return restartBear();
}

bool BearRender::restartBear()
{
    if(bearVbsAdapter) bearVbsAdapter.reset();
    if(bearRenderer) bearRenderer.reset();
    if(src != nullptr) {
        src_delete(src);
        src = nullptr;
    }

    originStartingFrame = -1; // -1 uninitialised
    originPlayheadTrackerFrames = 0;
    bearPlayheadOffsetSec = 0.0;
    onRenderInputStartFrame = -1;
    onRenderInputNumFrames = -1;
    onRenderOutputNumFrames = -1;
    outputGain = 1.0;

    if(!fileReadable(bearConfig.get_data_path())) {
        getExceptionHandler()->logException(std::string("Data file is inaccessible for read: ") + bearConfig.get_data_path());
        return false;
    }

    try {
        bearRenderer = std::make_shared<bear::Renderer>(bearConfig);
    } catch(std::exception &e) {
        getExceptionHandler()->logException(std::string("Error constructing bear::Renderer: ") + std::string(e.what()));
        return false;
    }

    try {
        bearVbsAdapter = std::make_unique<bear::VariableBlockSizeAdapter>(bearConfig, bearRenderer);
    } catch(std::exception &e) {
        getExceptionHandler()->logException(std::string("Error constructing bear::VariableBlockSizeAdapterQueue: ") + std::string(e.what()));
        bearRenderer.reset();
        return false;
    }

    try {
        bearRenderer->set_listener(bearListener);
    } catch(std::exception &e) {
        getExceptionHandler()->logException(std::string("Error assigning bear::Listener: ") + std::string(e.what()));
        // Can leave Renderer and VBS assigned - can still render, just no listener pos/ori.
        return false;
    }

    return true;
}

bool BearRender::prewarnBearRender(int startFrameAtOpSr, int numFramesAtOpSr, int opSampleRate, int useSrcType)
{
    bool retSuccess = true;
    betweenPrewarnAndRender = false; // Only true on success

    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    if(opSampleRate <= 0) {
        opSampleRate = bearConfig.get_sample_rate();
    }

    double srcRatio = (double)opSampleRate / (double)bearConfig.get_sample_rate();

    if(srcRatio == 1.0){
        onRenderInputStartFrame = startFrameAtOpSr;
        onRenderInputNumFrames = numFramesAtOpSr;
        onRenderOutputNumFrames = numFramesAtOpSr;

    } else {
        // Ensure SRC is configd

        srcData.src_ratio = srcRatio;
        srcData.end_of_input = 0;

        if(src != nullptr && srcType != useSrcType) {
            src_delete(src);
            src = nullptr;
        }

        if(src == nullptr) {
            int err = SRC_ERROR::SRC_ERR_NO_ERROR;
            src = src_new(useSrcType, 2, &err);
            if(err == SRC_ERROR::SRC_ERR_NO_ERROR) {
                srcType = useSrcType;
                // Prime filters

                std::vector<float> srcPrimingBuffer( (size_t)std::floor(((double)primingFrames * 2.0) / srcRatio), 0.0 );
                std::vector<float> srcPrimingDump( (primingFrames * 2), 0.0);
                srcData.data_in = srcPrimingBuffer.data();
                srcData.data_out = srcPrimingDump.data();
                srcData.input_frames = srcPrimingBuffer.size() / 2;
                srcData.output_frames = primingFrames;
                err = src_process(src, &srcData);
                if(err != SRC_ERROR::SRC_ERR_NO_ERROR) {
                    std::string errMsg{ "Failed to prime SRC: " };
                    errMsg += src_strerror(err);
                    getExceptionHandler()->logException(errMsg);
                    retSuccess = false;
                }

            } else {
                src_delete(src);
                src = nullptr;
                std::string errMsg{ "Failed to instantiate SRC: " };
                errMsg += src_strerror(err);
                getExceptionHandler()->logException(errMsg);
                retSuccess = false;
            }
        }

        // Inclusion of primingFrames in these calculations is because these frames have affected the state of the
        //  filter and so it may be partially though a frame, causing rounding error if it is not included in subsequent calcs
        double dblInputStartFrame = ((double)(startFrameAtOpSr + primingFrames)) / srcRatio;
        double dblInputNumFrames = (double)numFramesAtOpSr / srcRatio;
        int intInputEndFrame = (int)std::floor(dblInputStartFrame + dblInputNumFrames);
        onRenderInputStartFrame = (int)std::floor(dblInputStartFrame) - primingFrames;
        onRenderInputNumFrames = intInputEndFrame - onRenderInputStartFrame - primingFrames;
        onRenderOutputNumFrames = numFramesAtOpSr;

        srcData.output_frames = onRenderOutputNumFrames;
        srcData.input_frames = onRenderInputNumFrames;

    }

    if(originStartingFrame < 0) {
        // Never initialised
        originStartingFrame = onRenderInputStartFrame;
        originPlayheadTrackerFrames = onRenderInputStartFrame;
        bearPlayheadOffsetSec = (double)-originStartingFrame / (double)bearConfig.get_sample_rate();
    }
    else if(originPlayheadTrackerFrames != onRenderInputStartFrame) {
        // Must be contiguous requests!!!
        getExceptionHandler()->logException("Must have contiguous block requests! Expected start frame " + std::to_string(originPlayheadTrackerFrames) + ", but requested " + std::to_string(onRenderInputStartFrame));
        return false; // Can not continue in this case - return early
    }

    // Check our vectors are big enough
    if(onRenderInputNumFrames > onRenderOutputNumFrames){
        if(reusableZeroedChannel.size() < onRenderInputNumFrames) {
            setBufferFrameCounts(onRenderInputNumFrames);
        }
    } else {
        if(reusableZeroedChannel.size() < onRenderOutputNumFrames) {
            setBufferFrameCounts(onRenderInputNumFrames);
        }
    }

    betweenPrewarnAndRender = true;
    return retSuccess;
}

bool BearRender::addObjectMetadata(int forBearChannel, MetadataBlock* metadataBlock)
{
    getExceptionHandler()->clearException(); // Clear because this method can return false without an exception.

    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    if(!betweenPrewarnAndRender) {
        getExceptionHandler()->logException("BEAR must be prewarned before passing metadata!");
        return false;
    }

    if(forBearChannel < 0 || forBearChannel >= bearConfig.get_num_objects_channels()) {
        getExceptionHandler()->logException("BEAR channel number out of range for this input type!");
        return false;
    }

    // TODO: Cache latest bear::ObjectsInput generated and what it was generated from (metadataBlock pointer)
    // This way, if it is rejected, on the next call if the metadataBlock pointer matches, we can just use the one we already constructed.

    bear::ObjectsInput bearMetadata;
    double sampleRate = bearConfig.get_sample_rate();

    // Note offsetting rtime by originStartingFrame to enable seeking
    bearMetadata.rtime = bear::Time{ (int)(sampleRate * metadataBlock->rTime) - originStartingFrame, bearConfig.get_sample_rate() };
    if(metadataBlock->duration != INFINITY) {
        bearMetadata.duration = bear::Time{ (int)(sampleRate * metadataBlock->duration), bearConfig.get_sample_rate() };
    }

    // Bear only wants polar at the mo!
    bearMetadata.type_metadata.cartesian = false;
    bearMetadata.type_metadata.position = ear::PolarPosition{ metadataBlock->azimuth, metadataBlock->elevation, metadataBlock->distance };
    bearMetadata.type_metadata.gain = metadataBlock->gain;
    bearMetadata.type_metadata.diffuse = metadataBlock->diffuse;
    bearMetadata.type_metadata.height = metadataBlock->height;
    bearMetadata.type_metadata.width = metadataBlock->width;
    bearMetadata.type_metadata.depth = metadataBlock->depth;

    bearMetadata.interpolationLength = (metadataBlock->jumpPosition > 0) ? bear::Time{ (int)(sampleRate * metadataBlock->interpolationLength), bearConfig.get_sample_rate() } : bearMetadata.duration;
    bearMetadata.type_metadata.objectDivergence = ear::PolarObjectDivergence(metadataBlock->divergence, metadataBlock->divergenceAzimuthRange);
    bearMetadata.type_metadata.channelLock = ear::ChannelLock(metadataBlock->channelLock, metadataBlock->channelLockMaxDistance);
    bearMetadata.type_metadata.screenRef = metadataBlock->screenRef;

    if(metadataBlock->absoluteDistance != NAN && metadataBlock->absoluteDistance >= 0.0) {
        bearMetadata.audioPackFormat_data.absoluteDistance = metadataBlock->absoluteDistance;
    }

    return bearVbsAdapter->add_objects_block(onRenderInputNumFrames, forBearChannel, bearMetadata);
}

bool BearRender::addDirectSpeakersMetadata(int forBearChannel, MetadataBlock * metadataBlock)
{
    getExceptionHandler()->clearException(); // Clear because this method can return false without an exception.

    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    if(!betweenPrewarnAndRender) {
        getExceptionHandler()->logException("BEAR must be prewarned before passing metadata!");
        return false;
    }

    if(forBearChannel < 0 || forBearChannel >= bearConfig.get_num_direct_speakers_channels()) {
        getExceptionHandler()->logException("BEAR channel number out of range for this input type!");
        return false;
    }

    // TODO: Cache latest bear::DirectSpeakersInput generated and what it was generated from (metadataBlock pointer)
    // This way, if it is rejected, on the next call if the metadataBlock pointer matches, we can just use the one we already constructed.

    bear::DirectSpeakersInput bearMetadata;
    double sampleRate = bearConfig.get_sample_rate();

    bearMetadata.type_metadata.audioPackFormatID = metadataBlock->audioPackFormatId;

    // Note offsetting rtime by originStartingFrame to enable seeking
    bearMetadata.rtime = bear::Time{ (int)(sampleRate * metadataBlock->rTime) - originStartingFrame, bearConfig.get_sample_rate() };
    if(metadataBlock->duration != INFINITY) {
        bearMetadata.duration = bear::Time{ (int)(sampleRate * metadataBlock->duration), bearConfig.get_sample_rate() };
    }

    bearMetadata.type_metadata.position = ear::PolarSpeakerPosition{ metadataBlock->azimuth, metadataBlock->elevation, metadataBlock->distance };

    if(metadataBlock->absoluteDistance != NAN && metadataBlock->absoluteDistance >= 0.0) {
        bearMetadata.audioPackFormat_data.absoluteDistance = metadataBlock->absoluteDistance;
    }

    if(metadataBlock->lowPass != NAN) {
        bearMetadata.type_metadata.channelFrequency.lowPass = metadataBlock->lowPass;
    }

    if(metadataBlock->highPass != NAN) {
        bearMetadata.type_metadata.channelFrequency.highPass = metadataBlock->highPass;
    }

    if(metadataBlock->speakerLabel[0] != 0) { // First char not null... I.e, there is something
        bearMetadata.type_metadata.speakerLabels.push_back(std::string(metadataBlock->speakerLabel));
    }

    return bearVbsAdapter->add_direct_speakers_block(onRenderInputNumFrames, forBearChannel, bearMetadata);
}

bool BearRender::addHoaMetadata(int forBearChannels[], MetadataBlock * metadataBlock)
{
    getExceptionHandler()->clearException(); // Clear because this method can return false without an exception.

    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    if(!betweenPrewarnAndRender) {
        getExceptionHandler()->logException("BEAR must be prewarned before passing metadata!");
        return false;
    }

    // TODO: Cache latest bear::DirectSpeakersInput generated and what it was generated from (metadataBlock pointer)
    // This way, if it is rejected, on the next call if the metadataBlock pointer matches, we can just use the one we already constructed.

    bear::HOAInput bearMetadata;
    double sampleRate = bearConfig.get_sample_rate();

    // Note offsetting rtime by originStartingFrame to enable seeking
    bearMetadata.rtime = bear::Time{ (int)(sampleRate * metadataBlock->rTime) - originStartingFrame, bearConfig.get_sample_rate() };
    if(metadataBlock->duration != INFINITY) {
        bearMetadata.duration = bear::Time{ (int)(sampleRate * metadataBlock->duration), bearConfig.get_sample_rate() };
    }

    bearMetadata.channels.resize(metadataBlock->channelCount);
    bearMetadata.type_metadata.degrees.resize(metadataBlock->channelCount);
    bearMetadata.type_metadata.orders.resize(metadataBlock->channelCount);
    for(int i = 0; i < metadataBlock->channelCount; i++) {
        bearMetadata.channels[i] = forBearChannels[i];
        bearMetadata.type_metadata.degrees[i] = metadataBlock->degree[i];
        bearMetadata.type_metadata.orders[i] = metadataBlock->order[i];
    }

    bearMetadata.type_metadata.nfcRefDist = metadataBlock->nfcRefDist;
    bearMetadata.type_metadata.normalization = metadataBlock->normalisation;
    bearMetadata.type_metadata.screenRef = metadataBlock->screenRef;
    //TODO: not implemented; bearMetadata.type_metadata.referenceScreen

    bearMetadata.audioPackFormat_data.absoluteDistance = metadataBlock->absoluteDistance;

    return bearVbsAdapter->add_hoa_block(onRenderInputNumFrames, metadataBlock->id, bearMetadata);
}

bool BearRender::getBearRender(int objectInputChannelNums[], int objectInputChannelNumsSize,
                               int directSpeakersInputChannelNums[], int directSpeakersInputChannelNumsSize,
                               int hoaInputChannelNums[], int hoaInputChannelNumsSize,
                               float outputBuffer[])
{
    auto objectInputAudioBounds = std::vector<int>(objectInputChannelNumsSize * 2, 0);
    for(int channelIndex = 0; channelIndex < objectInputChannelNumsSize; channelIndex++){
        objectInputAudioBounds[channelIndex * 2 + 0] = 0;
        objectInputAudioBounds[channelIndex * 2 + 1] = INT_MAX;
    }

    auto directSpeakersInputAudioBounds = std::vector<int>(directSpeakersInputChannelNumsSize * 2, 0);
    for(int channelIndex = 0; channelIndex < directSpeakersInputChannelNumsSize; channelIndex++){
        directSpeakersInputAudioBounds[channelIndex * 2 + 0] = 0;
        directSpeakersInputAudioBounds[channelIndex * 2 + 1] = INT_MAX;
    }

    auto hoaInputAudioBounds = std::vector<int>(hoaInputChannelNumsSize * 2, 0);
    for(int channelIndex = 0; channelIndex < hoaInputChannelNumsSize; channelIndex++){
        hoaInputAudioBounds[channelIndex * 2 + 0] = 0;
        hoaInputAudioBounds[channelIndex * 2 + 1] = INT_MAX;
    }

    return getBearRenderBounded(objectInputChannelNums, objectInputAudioBounds.data(), objectInputChannelNumsSize,
                                directSpeakersInputChannelNums, directSpeakersInputAudioBounds.data(), directSpeakersInputChannelNumsSize,
                                hoaInputChannelNums, hoaInputAudioBounds.data(), hoaInputChannelNumsSize,
                                outputBuffer, 0, true);

}

bool BearRender::getBearRenderBounded(int objectInputChannelNums[], int objectInputAudioBounds[], int objectInputCount,
                                      int directSpeakersInputChannelNums[], int directSpeakersInputAudioBounds[], int directSpeakersInputCount,
                                      int hoaInputChannelNums[], int hoaInputAudioBounds[], int hoaInputCount,
                                      float outputBuffer[], int outputBufferStartFrame, bool outputOverwrite)
{
    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    if(!betweenPrewarnAndRender) {
        getExceptionHandler()->logException("BEAR must be prewarned before performing render!");
        return false;
    }
    betweenPrewarnAndRender = false;

    if(onRenderInputNumFrames <= 0) {
        getExceptionHandler()->logException("BEAR asked to render <= 0 frames!");
        return false;
    }

    bool resSuccess = true;

    // TODO - should probably warn if an "inputchannelnums" size > that in bearConfig (they are ignored)

    // Process
    /// Get BEAR input audio

    for(int channelIndex = 0; channelIndex < bearConfig.get_num_objects_channels(); channelIndex++) {
        if(channelIndex < objectInputCount) {
            int channelNum = objectInputChannelNums[channelIndex]; // No need to check within range - getAudioBlock does it
            if(!audioExtractor->getAudioBlock(onRenderInputStartFrame, onRenderInputNumFrames, &channelNum, 1, objectInputAudioBounds[channelIndex * 2], objectInputAudioBounds[channelIndex * 2 + 1], bearObjectInputBuffers[channelIndex]->data())) {
                return false; //getAudioBlock provides reason
            }
            bearObjectInputBuffers_RawPointers[channelIndex] = bearObjectInputBuffers[channelIndex]->data();
        } else {
            bearObjectInputBuffers_RawPointers[channelIndex] = reusableZeroedChannel.data();
        }
    }

    for(int channelIndex = 0; channelIndex < bearConfig.get_num_direct_speakers_channels(); channelIndex++) {
        if(channelIndex < directSpeakersInputCount) {
            int channelNum = directSpeakersInputChannelNums[channelIndex]; // No need to check within range - getAudioBlock does it
            if(!audioExtractor->getAudioBlock(onRenderInputStartFrame, onRenderInputNumFrames, &channelNum, 1, directSpeakersInputAudioBounds[channelIndex * 2], directSpeakersInputAudioBounds[channelIndex * 2 + 1], bearDirectSpeakersInputBuffers[channelIndex]->data())) {
                return false; //getAudioBlock provides reason
            }
            bearDirectSpeakersInputBuffers_RawPointers[channelIndex] = bearDirectSpeakersInputBuffers[channelIndex]->data();
        } else {
            bearDirectSpeakersInputBuffers_RawPointers[channelIndex] = reusableZeroedChannel.data();
        }
    }

    for(int channelIndex = 0; channelIndex < bearConfig.get_num_hoa_channels(); channelIndex++) {
        if(channelIndex < hoaInputCount) {
            int channelNum = hoaInputChannelNums[channelIndex]; // No need to check within range - getAudioBlock does it
            if(!audioExtractor->getAudioBlock(onRenderInputStartFrame, onRenderInputNumFrames, &channelNum, 1, hoaInputAudioBounds[channelIndex * 2], hoaInputAudioBounds[channelIndex * 2 + 1], bearHoaInputBuffers[channelIndex]->data())) {
                return false; //getAudioBlock provides reason
            }
            bearHoaInputBuffers_RawPointers[channelIndex] = bearHoaInputBuffers[channelIndex]->data();
        } else {
            bearHoaInputBuffers_RawPointers[channelIndex] = reusableZeroedChannel.data();
        }
    }

    /// Do BEAR process

    bearVbsAdapter->process(onRenderInputNumFrames,
                            bearObjectInputBuffers_RawPointers.data(),
                            bearDirectSpeakersInputBuffers_RawPointers.data(),
                            bearHoaInputBuffers_RawPointers.data(),
                            bearOutputBuffers_RawPointers.data());

    if(src) {
        /// Have an SRC set up - use it!

        for(int frameIndex = 0; frameIndex < onRenderInputNumFrames; frameIndex++)
        {
            int sampleOffsetForFrame = (frameIndex) * 2;
            srcInputBuffer[sampleOffsetForFrame + 0] = bearOutputBuffers[0]->at(frameIndex) * outputGain;
            srcInputBuffer[sampleOffsetForFrame + 1] = bearOutputBuffers[1]->at(frameIndex) * outputGain;
        }

        srcData.data_in = srcInputBuffer.data();
        srcData.data_out = srcOutputBuffer.data();
        src_process(src, &srcData);

        int outSamples = onRenderOutputNumFrames * 2;
        int sampleOffset = outputBufferStartFrame * 2;
        for(int sampleNum = 0; sampleNum < outSamples; sampleNum++)
        {
            if(outputOverwrite) {
                *(outputBuffer + sampleNum + sampleOffset) = srcOutputBuffer[sampleNum] * outputGain;
            } else {
                *(outputBuffer + sampleNum + sampleOffset) += srcOutputBuffer[sampleNum] * outputGain;
            }
        }

        if(srcData.output_frames_gen != srcData.output_frames) {
            // Mismatch
            getExceptionHandler()->logException("Output frame count mismatch! Expected " + std::to_string(srcData.output_frames) + ", but generated " + std::to_string(srcData.output_frames_gen));
            resSuccess = false;
        }

        if(srcData.input_frames_used != srcData.input_frames) {
            // Mismatch
            getExceptionHandler()->logException("Input frame consumption mismatch! Expected " + std::to_string(srcData.input_frames) + ", but consumed " + std::to_string(srcData.input_frames_used));
            resSuccess = false;
        }

    } else {

        /// No SRC - Copy samples directly to callers buffer in an interlaced fashion

        for(int frameIndex = 0; frameIndex < onRenderOutputNumFrames; frameIndex++)
        {
            int sampleOffsetForFrame = (frameIndex + outputBufferStartFrame) * 2;
            if(outputOverwrite) {
                *(outputBuffer + sampleOffsetForFrame + 0) = bearOutputBuffers[0]->at(frameIndex) * outputGain;
                *(outputBuffer + sampleOffsetForFrame + 1) = bearOutputBuffers[1]->at(frameIndex) * outputGain;
            } else {
                *(outputBuffer + sampleOffsetForFrame + 0) += bearOutputBuffers[0]->at(frameIndex) * outputGain;
                *(outputBuffer + sampleOffsetForFrame + 1) += bearOutputBuffers[1]->at(frameIndex) * outputGain;
            }
        }

    }

    // Done Process

    originPlayheadTrackerFrames += onRenderInputNumFrames;
    return resSuccess;
}

void BearRender::setOutputGain(float gain)
{
    outputGain = gain;
}

bool BearRender::getListenerLook(float * orientation_x, float * orientation_y, float * orientation_z)
{
    auto ret = bearListener.look();
    *orientation_x = ret[0];
    *orientation_y = ret[1];
    *orientation_z = ret[2];
    return true;
}

bool BearRender::getListenerUp(float * orientation_x, float * orientation_y, float * orientation_z)
{
    auto ret = bearListener.up();
    *orientation_x = ret[0];
    *orientation_y = ret[1];
    *orientation_z = ret[2];
    return true;
}

bool BearRender::getListenerRight(float * orientation_x, float * orientation_y, float * orientation_z)
{
    auto ret = bearListener.right();
    *orientation_x = ret[0];
    *orientation_y = ret[1];
    *orientation_z = ret[2];
    return true;
}

void BearRender::setBufferFrameCounts(size_t frameCount)
{
    reusableZeroedChannel = std::vector<float>(frameCount, 0.0);
    srcInputBuffer = std::vector<float>(frameCount, 0.0);
    srcOutputBuffer = std::vector<float>(frameCount, 0.0);

    for(int index = 0; index < bearOutputBuffers.size(); index++) {
        bearOutputBuffers[index] = std::make_shared<std::vector<float>>(frameCount, 0.0);
        bearOutputBuffers_RawPointers[index] = bearOutputBuffers[index]->data();
    }

    for(int index = 0; index < bearObjectInputBuffers.size(); index++) {
        bearObjectInputBuffers[index] = std::make_shared<std::vector<float>>(frameCount, 0.0);
        bearObjectInputBuffers_RawPointers[index] = reusableZeroedChannel.data();
    }

    for(int index = 0; index < bearDirectSpeakersInputBuffers.size(); index++) {
        bearDirectSpeakersInputBuffers[index] = std::make_shared<std::vector<float>>(frameCount, 0.0);
        bearDirectSpeakersInputBuffers_RawPointers[index] = reusableZeroedChannel.data();
    }

    for(int index = 0; index < bearHoaInputBuffers.size(); index++) {
        bearHoaInputBuffers[index] = std::make_shared<std::vector<float>>(frameCount, 0.0);
        bearHoaInputBuffers_RawPointers[index] = reusableZeroedChannel.data();
    }
}

bool BearRender::setListener(float position_x, float position_y, float position_z , float orientation_w, float orientation_x, float orientation_y, float orientation_z)
{
    if(!bearVbsAdapter || !bearRenderer) {
        getExceptionHandler()->logException("BEAR renderer or variable block size adapter not setup.");
        return false;
    }

    bearListener.set_position_cart(std::array<double, 3>{position_x, position_y, position_z});
    bearListener.set_orientation_quaternion(std::array<double, 4>{orientation_w, orientation_x, orientation_y, orientation_z});

    bearRenderer->set_listener(bearListener);
    return true;
}
