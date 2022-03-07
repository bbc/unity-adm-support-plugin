#include "main.h"

#include "Readers.h"
#include "BearRender.h"
#include "ExceptionHandler.h"

#include <limits.h>

#define CSHARP_BOOL uint32_t

extern "C"
{

    DLLEXPORT int readAdm(char filePath[2048])
    {
        return getFileReaderSingleton()->readAdm(filePath);
    }

    DLLEXPORT const char* getLatestException()
    {
        return getExceptionHandler()->getLatestException();
    }

    DLLEXPORT int discoverNewRenderableItems()
    {
        auto metadataExtractor = getFileReaderSingleton()->getMetadata();
        if(!metadataExtractor) {
            getExceptionHandler()->logException("Library Error: No metadataExtractor initialised!");
            return 0;
        }
        return metadataExtractor->discoverNewRenderableItems();
    }

    DLLEXPORT CSHARP_BOOL getNextMetadataBlock(MetadataBlock* metadataBlock)
    {
        auto metadataExtractor = getFileReaderSingleton()->getMetadata();
        if(!metadataExtractor) {
            getExceptionHandler()->logException("Library Error: No metadataExtractor initialised!");
            return false;
        }
        return metadataExtractor->getNextMetadataBlock(metadataBlock);
    }

    DLLEXPORT int getSampleRate()
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return 0;
        }
        return audioExtractor->getSampleRate();
    }

    DLLEXPORT int getNumberOfFrames()
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return 0;
        }
        return audioExtractor->getNumberOfFrames();
    }

    DLLEXPORT CSHARP_BOOL getAudioBlockBounded(int startFrame, int numFrames, int channelNums[], int channelNumsSize, int lowerFrameBound, int upperFrameBound, float outputBuffer[])
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return false;
        }
        return audioExtractor->getAudioBlock(startFrame, numFrames, channelNums, channelNumsSize, lowerFrameBound, upperFrameBound, outputBuffer);
    }

    DLLEXPORT CSHARP_BOOL getAudioBlock(int startFrame, int numFrames, int channelNums[], int channelNumsSize, float outputBuffer[])
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return false;
        }
        return audioExtractor->getAudioBlock(startFrame, numFrames, channelNums, channelNumsSize, 0, INT_MAX, outputBuffer);
    }

    // BEAR

    DLLEXPORT CSHARP_BOOL setupBear(int maxObjectsChannels, int maxDirectSpeakersChannels, int maxHoaChannels)
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return false;
        }
        return getBearSingleton()->setupBear(audioExtractor, maxObjectsChannels, maxDirectSpeakersChannels, maxHoaChannels);
    }

    DLLEXPORT CSHARP_BOOL setupBearEx(int maxObjectsChannels, int maxDirectSpeakersChannels, int maxHoaChannels, int maxAnticipatedBlockFrameRequest, int rendererInternalBlockFrameCount, char dataPath[2048], char fftImpl[64])
    {
        auto audioExtractor = getFileReaderSingleton()->getAudio();
        if(!audioExtractor) {
            getExceptionHandler()->logException("Library Error: No audioExtractor initialised!");
            return false;
        }
        return getBearSingleton()->setupBear(audioExtractor, maxObjectsChannels, maxDirectSpeakersChannels, maxHoaChannels, maxAnticipatedBlockFrameRequest, rendererInternalBlockFrameCount, std::string{ dataPath }, std::string{ fftImpl });
    }

    DLLEXPORT CSHARP_BOOL restartBear()
    {
        return getBearSingleton()->restartBear();
    }

    DLLEXPORT CSHARP_BOOL prewarnBearRender(int startFrame, int numFrames)
    {
        return getBearSingleton()->prewarnBearRender(startFrame, numFrames);
    }

    DLLEXPORT CSHARP_BOOL prewarnBearRenderSrc(int startFrame, int numFrames, int basedOnSampleRate, int srcType)
    {
        return getBearSingleton()->prewarnBearRender(startFrame, numFrames, basedOnSampleRate, srcType);
    }

    DLLEXPORT CSHARP_BOOL getBearRender(int objectInputChannelNums[], int objectInputChannelNumsSize,
                                        int directSpeakersInputChannelNums[], int directSpeakersInputChannelNumsSize,
                                        int hoaInputChannelNums[], int hoaInputChannelNumsSize,
                                        float outputBuffer[])
    {
        return getBearSingleton()->getBearRender(objectInputChannelNums, objectInputChannelNumsSize,
                                                 directSpeakersInputChannelNums, directSpeakersInputChannelNumsSize,
                                                 hoaInputChannelNums, hoaInputChannelNumsSize,
                                                 outputBuffer);
    }

    DLLEXPORT CSHARP_BOOL getBearRenderBounded(int objectInputChannelNums[], int objectInputAudioBounds[], int objectInputCount,
                                               int directSpeakersInputChannelNums[], int directSpeakersInputAudioBounds[], int directSpeakersInputCount,
                                               int hoaInputChannelNums[], int hoaInputAudioBounds[], int hoaInputCount,
                                               float outputBuffer[], int outputBufferStartFrame, CSHARP_BOOL outputOverwrite)
    {
        return getBearSingleton()->getBearRenderBounded(objectInputChannelNums, objectInputAudioBounds, objectInputCount,
                                                 directSpeakersInputChannelNums, directSpeakersInputAudioBounds, directSpeakersInputCount,
                                                 hoaInputChannelNums, hoaInputAudioBounds, hoaInputCount,
                                                 outputBuffer, outputBufferStartFrame, outputOverwrite);
    }

    DLLEXPORT void setBearOutputGain(float gain)
    {
        getBearSingleton()->setOutputGain(gain);
    }

    DLLEXPORT CSHARP_BOOL addBearObjectMetadata(int forBearChannel, MetadataBlock* metadataBlock)
    {
        return getBearSingleton()->addObjectMetadata(forBearChannel, metadataBlock);
    }

    DLLEXPORT CSHARP_BOOL addBearDirectSpeakersMetadata(int forBearChannel, MetadataBlock* metadataBlock)
    {
        return getBearSingleton()->addDirectSpeakersMetadata(forBearChannel, metadataBlock);
    }

    DLLEXPORT CSHARP_BOOL addBearHoaMetadata(int forBearChannels[], MetadataBlock* metadataBlock)
    {
        return getBearSingleton()->addHoaMetadata(forBearChannels, metadataBlock);
    }

    DLLEXPORT CSHARP_BOOL setListener(float position_x, float position_y, float position_z , float orientation_w, float orientation_x, float orientation_y, float orientation_z)
    {
        return getBearSingleton()->setListener(position_x, position_y, position_z , orientation_w, orientation_x, orientation_y, orientation_z);
    }

    DLLEXPORT CSHARP_BOOL getListenerLook(float* orientation_x, float* orientation_y, float* orientation_z)
    {
        return getBearSingleton()->getListenerLook(orientation_x, orientation_y, orientation_z);
    }

    DLLEXPORT CSHARP_BOOL getListenerUp(float* orientation_x, float* orientation_y, float* orientation_z)
    {
        return getBearSingleton()->getListenerUp(orientation_x, orientation_y, orientation_z);
    }

    DLLEXPORT CSHARP_BOOL getListenerRight(float* orientation_x, float* orientation_y, float* orientation_z)
    {
        return getBearSingleton()->getListenerRight(orientation_x, orientation_y, orientation_z);
    }
}


