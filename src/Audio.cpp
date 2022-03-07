#include "Audio.h"
#include "Readers.h"
#include "ExceptionHandler.h"

Bw64AudioExtractor::Bw64AudioExtractor(FileReader * parentFileReader) : fileReader{ parentFileReader }
{
}

Bw64AudioExtractor::~Bw64AudioExtractor()
{
}

int Bw64AudioExtractor::getSampleRate()
{
    auto bw64Reader = fileReader->getReader();
    if(!bw64Reader) return 0;
    return bw64Reader->sampleRate();
}

int Bw64AudioExtractor::getNumberOfFrames()
{
    auto bw64Reader = fileReader->getReader();
    if(!bw64Reader) return 0;
    return bw64Reader->numberOfFrames();
}

bool Bw64AudioExtractor::getAudioBlock(int startFrame, int numFrames, int channelNums[], int channelNumsSize, int lowerFrameBound, int upperFrameBound, float outputBuffer[])
{
    auto bw64Reader = fileReader->getReader();
    if(!bw64Reader) {
        getExceptionHandler()->logException("No Reader available to extract audio!");
        return false;
    }
    int availableChannels = bw64Reader->channels();

    if(startFrame < latestExtractedAudioBlock_StartFrame || (startFrame + numFrames) > latestExtractedAudioBlock_EndFrame) {
        // Need to extract new block
        newBlockExtractCounter++;

        // Get cache vector ready
        size_t lookBehindFrames= std::ceil(lookBehindSec * (float)bw64Reader->sampleRate());
        size_t lookAheadFrames = std::ceil(lookAheadSec * (float)bw64Reader->sampleRate());
        latestExtractedAudioBlock_StartFrame = startFrame - lookBehindFrames;
        latestExtractedAudioBlock_EndFrame = std::max(startFrame + numFrames, (int)(startFrame + lookAheadFrames));
        latestExtractedAudioBlock_FrameCount = latestExtractedAudioBlock_EndFrame - latestExtractedAudioBlock_StartFrame;
        size_t reqSize = latestExtractedAudioBlock_FrameCount * availableChannels;
        if(latestExtractedAudioBlock.size() < reqSize) {
            latestExtractedAudioBlock = std::vector<float>(reqSize); // Could resize but not interested in preserving existing data
        }

        //Start - Calculate buffer section and padding
        int remainingBuffer = latestExtractedAudioBlock_FrameCount;

        int prePaddingFrameCount = 0;
        if(latestExtractedAudioBlock_StartFrame < 0) prePaddingFrameCount = -latestExtractedAudioBlock_StartFrame;
        if(prePaddingFrameCount > remainingBuffer) prePaddingFrameCount = remainingBuffer;

        remainingBuffer -= prePaddingFrameCount;

        int readFrameStart = latestExtractedAudioBlock_StartFrame;
        if(readFrameStart < 0) readFrameStart = 0;
        int readFrameEnd = readFrameStart + remainingBuffer;
        if(readFrameEnd > bw64Reader->numberOfFrames()) readFrameEnd = bw64Reader->numberOfFrames();
        if(readFrameEnd < readFrameStart) readFrameEnd = readFrameStart;
        int readFrameCount = readFrameEnd - readFrameStart;
        if(readFrameCount > remainingBuffer) {
            readFrameCount = remainingBuffer;
            readFrameEnd = readFrameStart + readFrameCount;
        }

        remainingBuffer -= readFrameCount;

        int postPaddingFrameCount = remainingBuffer;
        assert(postPaddingFrameCount >= 0);
        ///End - Calculate buffer section and padding

        size_t outputSampleIndex = 0;

        // Pre-padding
        int prePaddingSampleCount = prePaddingFrameCount * availableChannels;
        for(int sampleNum = 0; sampleNum < prePaddingSampleCount; sampleNum++)
        {
            latestExtractedAudioBlock[outputSampleIndex] = 0.0;
            outputSampleIndex++;
        }

        // Audio samples
        if(readFrameCount > 0) {
            bw64Reader->seek(readFrameStart);
            bw64Reader->read((latestExtractedAudioBlock.data() + outputSampleIndex), readFrameCount);
            outputSampleIndex += readFrameCount * availableChannels;
        }

        // Post-padding
        int postPaddingSampleCount = postPaddingFrameCount * availableChannels;
        for(int sampleNum = 0; sampleNum < postPaddingSampleCount; sampleNum++)
        {
            latestExtractedAudioBlock[outputSampleIndex] = 0.0;
            outputSampleIndex++;
        }

    } else {
        // Reuse existing cached block
        reuseBlockCounter++;
    }

    // Extract just the channels we want
    float* bufferPosition = outputBuffer;
    //int64_t prevents overflows on bounds
    int64_t relStart = startFrame - latestExtractedAudioBlock_StartFrame;
    int64_t relEnd = relStart + numFrames;
    int64_t relLowerFrameBound = (int64_t)lowerFrameBound - (int64_t)latestExtractedAudioBlock_StartFrame;
    int64_t relUpperFrameBound = (int64_t)upperFrameBound - (int64_t)latestExtractedAudioBlock_StartFrame;

    for(int64_t relFrameNum = relStart; relFrameNum < relEnd; relFrameNum++)
    {
        int originFrameNum = latestExtractedAudioBlock_StartFrame + relFrameNum;
        bool inBounds = relFrameNum >= relLowerFrameBound && relFrameNum <= relUpperFrameBound;
        for(int channelIndex = 0; channelIndex < channelNumsSize; channelIndex++)
        {
            if(inBounds) {
                auto channelNum = channelNums[channelIndex];
                if(channelNum >= 0 && channelNum < availableChannels) {
                    int64_t blockPos = channelNum + (relFrameNum * availableChannels);
                    *bufferPosition = latestExtractedAudioBlock[blockPos];
                } else {
                    // TODO - should probably warn somehow. Requested channel isn't in the file.
                    *bufferPosition = 0.0;
                }
            } else {
                *bufferPosition = 0.0;
            }
            bufferPosition++;
        }
    }

    return true;
}
