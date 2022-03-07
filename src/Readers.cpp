#include "Readers.h"
#include "Helpers.h"
#include "ExceptionHandler.h"

namespace {
    FileReader* fileReader = nullptr;
}

FileReader* getFileReaderSingleton()
{
    // TODO - need a destroy method to tidy this up when unloading lib
    if(!fileReader) {
        fileReader = new FileReader();
    }
    return fileReader;
}

FileReader::FileReader()
{
}

FileReader::~FileReader()
{
}

std::shared_ptr<bw64::Bw64Reader> FileReader::getReader()
{
    return bw64Reader;
}

std::shared_ptr<AudioExtractor> FileReader::getAudio()
{
    return audioExtractor;
}

std::shared_ptr<MetadataExtractor> FileReader::getMetadata()
{
    return metadataExtractor;
}

int FileReader::readAdm(char filePath[2048])
{
    bw64Reader = nullptr;
    parsedDocument = nullptr;
    audioExtractor.reset();
    metadataExtractor.reset();

    try
    {
        bw64Reader = bw64::readFile(filePath);
        auto aXml = bw64Reader->axmlChunk();
        auto chnaChunk = bw64Reader->chnaChunk();
        audioIds = chnaChunk->audioIds();

        std::stringstream stream;
        aXml->write(stream);
        parsedDocument = adm::parseXml(stream);
    }
    catch (std::exception &e)
    {
        getExceptionHandler()->logException(e.what());
        return 1;
    }

    reflectChnaRefsInAdm();
    audioExtractor = std::make_unique<Bw64AudioExtractor>(this);
    metadataExtractor = std::make_unique<MetadataExtractor>(this, parsedDocument);
    return 0;
}

void FileReader::reflectChnaRefsInAdm()
{
    // Some refs may only be provided in the CHNA, which is no good for our 'universal' metadata extractor.
    // Therefore ensure these refs are present in the ADM.

    /// Some older (and technically incorrect) ADM files only provide audioTrackUid->AudioTrackFormat refs in the CHNA chunk
    for(auto& audioId : audioIds)
    {
        auto audioTrackUidId = adm::parseAudioTrackUidId(audioId.uid());
        auto audioTrackUid = parsedDocument->lookup(audioTrackUidId);
        if(!audioTrackUid) continue;

        auto audioTrackFormatId = adm::parseAudioTrackFormatId(audioId.trackRef());
        auto audioTrackFormat = parsedDocument->lookup(audioTrackFormatId);
        if(!audioTrackFormat) continue;

        audioTrackUid->setReference(audioTrackFormat);
    }
}


int FileReader::getChannelNumFor(std::shared_ptr<adm::AudioTrackUid> audioTrackUid)
{
    // This will vary depending on how mappings are provided for a Filebased or S-ADM stream, so has to be part of the reader.

    /// In this case... Lookup in CHNA for file-based
    auto targetAudioTrackUidRefStr = adm::formatId(audioTrackUid->get<adm::AudioTrackUidId>());
    for(auto& audioId : audioIds)
    {
        auto audioTrackUidRefStr = audioId.uid();
        if(targetAudioTrackUidRefStr == audioTrackUidRefStr) {
            return audioId.trackIndex() - 1; // trackIndex is 1-based
        }
    }

    return -1; // -1 = not found
}
