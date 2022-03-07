#pragma once

#include <bear/api.hpp>
#include <bear/variable_block_size.hpp>
#include <samplerate.h>
#include "Audio.h"
#include "Metadata.h"

class BearRender
{
public:
    BearRender();
    ~BearRender();

    // All of these methods need access from outside the library

    bool setupBear(std::shared_ptr<AudioExtractor> usingExtractor,
                   size_t maxObjectsChannels = 64,
                   size_t maxDirectSpeakersChannels = 64,
                   size_t maxHoaChannels = 64,
                   size_t maxAnticipatedBlockFrameRequest = 4096,
                   size_t rendererInternalBlockFrameCount = 1024,
                   std::string dataPath = "",
                   std::string fft = "");
    bool restartBear();

    bool prewarnBearRender(int startFrame, int numFrames, int basedOnSampleRate = 0, int useSrcType = SRC_SINC_MEDIUM_QUALITY);

    bool addObjectMetadata(int forBearChannel, MetadataBlock* metadataBlock);
    bool addDirectSpeakersMetadata(int forBearChannel, MetadataBlock* metadataBlock);
    bool addHoaMetadata(int forBearChannels[], MetadataBlock* metadataBlock);

    bool getBearRender(int objectInputChannelNums[], int objectInputChannelNumsSize,
                       int directSpeakersInputChannelNums[], int directSpeakersInputChannelNumsSize,
                       int hoaInputChannelNums[], int hoaInputChannelNumsSize,
                       float outputBuffer[]);

    bool getBearRenderBounded(int objectInputChannelNums[], int objectInputAudioBounds[], int objectInputCount,
                              int directSpeakersInputChannelNums[], int directSpeakersInputAudioBounds[], int directSpeakersInputCount,
                              int hoaInputChannelNums[], int hoaInputAudioBounds[], int hoaInputCount,
                              float outputBuffer[], int outputBufferStartFrame, bool outputOverwrite);

    bool setListener(float position_x, float position_y, float position_z , float orientation_w, float orientation_x, float orientation_y, float orientation_z);
    void setOutputGain(float gain);

    // Test methods to determine coordinate system
    bool getListenerLook(float* orientation_x, float* orientation_y, float* orientation_z);
    bool getListenerUp(float* orientation_x, float* orientation_y, float* orientation_z);
    bool getListenerRight(float* orientation_x, float* orientation_y, float* orientation_z);

private:
    std::shared_ptr<AudioExtractor> audioExtractor;

    int originStartingFrame{ -1 };          // Which frame we started reading from in the source audios... (-1 uninitialised)
    int originPlayheadTrackerFrames{ 0 };   // Where the read head is now in the source audio
    double bearPlayheadOffsetSec{ 0.0 };    // originStartingFrame in terms of seconds

    int onRenderInputNumFrames{ -1 };       // Number of frames of source audios to feed in to bear on render
    int onRenderInputStartFrame{ -1 };      // Where to begin pulling source audios from to feed in to bear on render
    int onRenderOutputNumFrames{ -1 };      // Number of frames after SRC (note that post-bear (pre-src), its still onRenderInputNumFrames - bear spits out as many as it took in)

    float outputGain{ 1.0 };

    bear::Config bearConfig;
    std::shared_ptr<bear::Renderer> bearRenderer;
    std::unique_ptr<bear::VariableBlockSizeAdapter> bearVbsAdapter;
    bear::Listener bearListener;

    //SRC
    SRC_STATE* src{ nullptr };
    SRC_DATA srcData;
    int srcType;
    std::vector<float> srcInputBuffer;
    std::vector<float> srcOutputBuffer;
    int primingFrames{ 256 }; // 144 samples seems to be enough for longest sinc filter - 256 to be safe

    // Bear temp buffers - Save redeclaring on each process call
    std::vector<float> reusableZeroedChannel;
    std::vector<std::shared_ptr<std::vector<float>>> bearOutputBuffers; // Wrap in smart pointers to manage lifetime
    std::vector<float*> bearOutputBuffers_RawPointers; // Bear wants array of raw pointers
    std::vector<std::shared_ptr<std::vector<float>>> bearObjectInputBuffers;
    std::vector<float*> bearObjectInputBuffers_RawPointers;
    std::vector<std::shared_ptr<std::vector<float>>> bearDirectSpeakersInputBuffers;
    std::vector<float*> bearDirectSpeakersInputBuffers_RawPointers;
    std::vector<std::shared_ptr<std::vector<float>>> bearHoaInputBuffers;
    std::vector<float*> bearHoaInputBuffers_RawPointers;
    void setBufferFrameCounts(size_t frameCount);

    bool betweenPrewarnAndRender{ false };

};

BearRender* getBearSingleton();
