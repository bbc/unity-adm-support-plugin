#pragma once
#include <memory>
#include <string>
#include <optional>
#include <adm/adm.hpp>
#include "Helpers.h"

using RenderableItemId = uint64_t;
using RenderableItemChannelId = uint64_t;
struct RenderableItemChannel;

struct RenderableItem {
    RenderableItemId selfId;
    std::string presentedName;
    adm::TypeDescriptor typeDefinition;
    std::shared_ptr<adm::AudioProgramme> audioProgramme;
    std::shared_ptr<adm::AudioContent> audioContent;
    std::vector<std::shared_ptr<adm::AudioObject>> audioObjectTree;
    std::map<RenderableItemChannelId, std::shared_ptr<RenderableItemChannel>> renderableItemChannels;
    double startTime;
    double duration;
    double endTime;
};

struct RenderableItemChannel {
    RenderableItemChannelId selfId;
    std::shared_ptr<RenderableItem> renderableItem;
    std::string audioPackFormatId;
    std::vector<std::shared_ptr<adm::AudioPackFormat>> audioPackFormatTree;
    bool valid;
    int channelNum;
    int lastSentBlockIndex;
    std::shared_ptr<adm::AudioTrackUid> audioTrackUid;
    std::shared_ptr<adm::AudioChannelFormat> audioChannelFormat;
    std::shared_ptr<adm::AudioStreamFormat> audioStreamFormat;
    std::shared_ptr<adm::AudioTrackFormat> audioTrackFormat;
    double highPass;
    double lowPass;
    double absoluteDistance;
};

struct MetadataBlock
{

    // Useful within Unity
    uint64_t id;
    uint8_t channelCount;
    uint8_t channelNums[64];
    char name[100];
    uint8_t typeDef;

    double audioStartTime;
    double audioEndTime;

    // Rest of these are values required by BEAR metadata classes (and we can also use for native Unity rendering)

    char audioPackFormatId[12];     // DirectSpeakers - audioPackFormatID

    double rTime;                   // Common (MetadataInput)
    double duration;                // Common (MetadataInput)

    bool jumpPosition;              // Object (ObjectsInput - whether interpolationLength optional is set)
    double interpolationLength;     // Object (ObjectsInput)

    bool cartesian;                 // Object (ObjectsTypeMetadata)
    double x;                       // Object (Position)
    double y;                       // Object (Position)
    double z;                       // Object (Position)
    double azimuth;                 // Object (Position), DirectSpeakers (PolarSpeakerPosition)
    double elevation;               // Object (Position), DirectSpeakers (PolarSpeakerPosition)
    double distance;                // Object (Position), DirectSpeakers (PolarSpeakerPosition)
    double absoluteDistance;        // PackFormat - reference distance

    double width;                   // Object (ObjectsTypeMetadata)
    double height;                  // Object (ObjectsTypeMetadata)
    double depth;                   // Object (ObjectsTypeMetadata)
    double gain;                    // ADM Commmon + Object (ObjectsTypeMetadata)
    double diffuse;                 // Object (ObjectsTypeMetadata)

    double divergence;              // Object (ObjectDivergence)
    double divergenceAzimuthRange;  // Object (ObjectDivergence)
    double divergencePositionRange; // Object (ObjectDivergence)

    bool channelLock;               // Object (ChannelLock)
    double channelLockMaxDistance;  // Object (ChannelLock)

    bool screenRef;                 // Object (ObjectsTypeMetadata), HOA (HOATypeMetadata)

    double highPass;                // DirectSpeakers - ChannelFrequency
    double lowPass;                 // DirectSpeakers - ChannelFrequency
    char speakerLabel[64];          // DirectSpeakers - speakerLabels

    char normalisation[8];          // HOA (HOATypeMetadata)
    int8_t order[64];               // HOA (HOATypeMetadata)
    int8_t degree[64];              // HOA (HOATypeMetadata)
    double nfcRefDist;              // HOA (HOATypeMetadata)

                                    // Object/Hoa - ignored Screen          -   bear::ObjectsInput.type_metadata.referenceScreen - Not part of AudioBlockFormat, could be extracted, but let's just stick to default screen support for now.
                                    // Object - ignored ZoneExclusion   -   bear::ObjectsInput.type_metadata.zoneExclusion - requires vector which can't be passed easily via Unity.

                                    // Binaural - needs to work differently;
                                    //      No blocks for binaural - we just use the channelformat name
                                    //      Will need piping directly to audio output - not via any renderer
};

class Reader;

class MetadataExtractor
{
    // This class is designed to be reusable with S-ADM

public:
    MetadataExtractor(Reader* parentReader, std::shared_ptr<adm::Document> parsedDocument);
    ~MetadataExtractor();

    int discoverNewRenderableItems();

    bool getNextMetadataBlock(MetadataBlock* metadataBlock); // MetadataBlock = Universal struct for all type defs.
                                                             //When C# calls this method, it should provide a pointer to an equivalent struct to populate from here.
                                                             // Return is whether new metadata was able to be sent (i.e, available).

private:
    Reader* parentReader;
    std::shared_ptr<adm::Document> parsedDocument;

    std::map<RenderableItemId, std::shared_ptr<RenderableItem>> renderableItems;
    std::map<RenderableItemChannelId, std::shared_ptr<RenderableItemChannel>> renderableItemChannels;
    std::vector<std::shared_ptr<RenderableItem>> validRenderableItems; // Used for a quick iterable for sending metadata blocks
    int idIndexOfLastRenderableItemSent{ -1 };

    RenderableItemChannelId generateRenderableItemChannelId(std::shared_ptr<adm::AudioTrackUid> trackUid);
    RenderableItemId generateRenderableItemId(std::shared_ptr<adm::AudioObject> audioObject, std::shared_ptr<adm::AudioTrackUid> trackUid);
    int discoverViaAudioProgramme(std::shared_ptr<adm::AudioProgramme> audioProgramme);
    int discoverViaAudioContent(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent);
    int discoverViaAudioObject(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent, std::vector<std::shared_ptr<adm::AudioObject>> audioObjectTree);
    int discoverFromAudioTrackUid(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent, std::vector<std::shared_ptr<adm::AudioObject>> audioObjectTree, std::shared_ptr<adm::AudioTrackUid> audioTrackUid);
    std::optional<std::vector<std::shared_ptr<adm::AudioPackFormat>>> tracePackFormatTree(std::shared_ptr<adm::AudioPackFormat> fromPackFormat, std::shared_ptr<adm::AudioChannelFormat> toChannelFormat, std::vector<std::shared_ptr<adm::AudioPackFormat>> history = std::vector<std::shared_ptr<adm::AudioPackFormat>>());


    void populateTypeSpecificMetadata(MetadataBlock* metadataBlock, adm::AudioBlockFormatObjects* audioBlockFormat, std::shared_ptr<RenderableItemChannel> renderableItemChannel);
    void populateTypeSpecificMetadata(MetadataBlock* metadataBlock, adm::AudioBlockFormatDirectSpeakers* audioBlockFormat, std::shared_ptr<RenderableItemChannel> renderableItemChannel);
    void populateHoaSpecificMetadata(MetadataBlock* metadataBlock, adm::AudioBlockFormatHoa* refAudioBlockFormat, std::shared_ptr<RenderableItem> renderableItem);
};