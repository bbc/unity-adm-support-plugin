#pragma once
#include <memory>
#include <string>
#include <adm/adm.hpp>
#include "Helpers.h"

class Reader; // Forward decl

class AudioExtractor
{
public:
    AudioExtractor() {};
    ~AudioExtractor() {};

    virtual int getSampleRate() = 0;
    virtual int getNumberOfFrames() = 0;
    virtual bool getAudioBlock(int startFrame, int numFrames, int channelNums[], int channelNumsSize, int lowerFrameBound, int upperFrameBound, float outputBuffer[]) = 0;
};

class FileReader; // Forward decl

class Bw64AudioExtractor : public AudioExtractor
{
public:
    Bw64AudioExtractor(FileReader* parentReader);
    ~Bw64AudioExtractor();

    int getSampleRate() override;
    int getNumberOfFrames() override;

    bool getAudioBlock(int startFrame, int numFrames, int channelNums[], int channelNumsSize, int lowerFrameBound, int upperFrameBound, float outputBuffer[]) override;

private:
    FileReader* fileReader;

    // Very high probability there will be multiple sequential requests for channels of audio from the same block in the file
    // Therefore, cache the latest extracted block... saves repeatedly declaring buffer, seeking, and reading (inc costly decoding).
    std::vector<float> latestExtractedAudioBlock{};
    int latestExtractedAudioBlock_StartFrame{ 0 };
    int latestExtractedAudioBlock_EndFrame{ 0 };
    int latestExtractedAudioBlock_FrameCount{ 0 };
    // Note that unitys default look-ahead seems to be 800ms.. we allow more for safety, be we need to include this 'extra' in look-behind,
    // otherwise we'll end up having to load an older block again when it moves on to the next channel, because we don't have that audio!
    float lookBehindSec{ 0.2 };
    float lookAheadSec{ 1.0 };
    // Just stats stuff, out of interest
    uint64_t newBlockExtractCounter{ 0 };
    uint64_t reuseBlockCounter{ 0 };

};