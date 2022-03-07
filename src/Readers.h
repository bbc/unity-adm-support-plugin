#pragma once

#include <adm/adm.hpp>
#include <bw64/bw64.hpp>
#include <adm/parse.hpp>
#include <vector>
#include <memory>
#include <string>
#include "Audio.h"
#include "Metadata.h"

class Reader
{
    // Interface for Readers (parent classes) - whether file based or S-ADM
public:
    Reader() {};
    ~Reader() {};

    virtual std::shared_ptr<AudioExtractor> getAudio() = 0;
    virtual std::shared_ptr<MetadataExtractor> getMetadata() = 0;
    virtual int getChannelNumFor(std::shared_ptr<adm::AudioTrackUid> audioTrackUid) = 0;
};

class FileReader : public Reader
{
public:
    FileReader();
    ~FileReader();

    std::shared_ptr<bw64::Bw64Reader> getReader();
    std::shared_ptr<AudioExtractor> getAudio() override;
    std::shared_ptr<MetadataExtractor> getMetadata() override;

    int getChannelNumFor(std::shared_ptr<adm::AudioTrackUid> audioTrackUid) override;

    int readAdm(char filePath[2048]);

private:
    std::shared_ptr<adm::Document> parsedDocument;
    std::shared_ptr<bw64::Bw64Reader> bw64Reader;
    std::vector<bw64::AudioId> audioIds;
    std::shared_ptr<Bw64AudioExtractor> audioExtractor;
    std::shared_ptr<MetadataExtractor> metadataExtractor;

    void reflectChnaRefsInAdm();
};

FileReader* getFileReaderSingleton();
