#include "Metadata.h"
#include "Helpers.h"
#include "Readers.h"
#include "ExceptionHandler.h"
#include <algorithm>

std::string MetadataExtractor::generatePresentedName(std::vector<std::shared_ptr<adm::AudioObject>> &audioObjectTree, std::vector<std::shared_ptr<adm::AudioPackFormat>> &audioPackFormatTree, std::shared_ptr<adm::AudioChannelFormat> audioChannelFormat, adm::TypeDescriptor typeDefinition) {
    std::string presentedName{};
    std::string audioObjectName{};
    std::string audioPackFormatName{};
    std::string audioChannelFormatName{};
    if(audioObjectTree.size() > 0 && audioObjectTree.back()->has<adm::AudioObjectName>()) {
        audioObjectName = audioObjectTree.back()->get<adm::AudioObjectName>().get();
    }
    if(typeDefinition == adm::TypeDefinition::DIRECT_SPEAKERS || typeDefinition == adm::TypeDefinition::OBJECTS) {
        // These assets are treated as independent channels
        if(audioPackFormatTree.size() > 0 && audioPackFormatTree.back()->has<adm::AudioPackFormatName>()) {
            audioPackFormatName = audioPackFormatTree.back()->get<adm::AudioPackFormatName>().get();
        }
        if(audioChannelFormat && audioChannelFormat->has<adm::AudioChannelFormatName>()) {
            audioChannelFormatName = audioChannelFormat->get<adm::AudioChannelFormatName>().get();
        }
    }
    if(audioObjectName.length() > 0) {
        presentedName = audioObjectName;
    }
    if(audioPackFormatName.length() > 0 && audioPackFormatName != audioObjectName) {
        if(presentedName.length() > 0) {
            presentedName += " -> ";
        }
        presentedName += audioPackFormatName;
    }
    if(audioChannelFormatName.length() > 0 && audioChannelFormatName != audioPackFormatName) {
        if(presentedName.length() > 0) {
            presentedName += " -> ";
        }
        presentedName += audioChannelFormatName;
    }
    if(presentedName.length() == 0) {
        presentedName = "(Unknown)";
    }
    return presentedName;
}

RenderableItemChannelId MetadataExtractor::generateRenderableItemChannelId(std::shared_ptr<adm::AudioTrackUid> trackUid)
{
    return trackUid->get<adm::AudioTrackUidId>().get<adm::AudioTrackUidIdValue>().get();
}

RenderableItemId MetadataExtractor::generateRenderableItemId(std::shared_ptr<adm::AudioObject> audioObject, std::shared_ptr<adm::AudioTrackUid> trackUid)
{
    RenderableItemChannelId op = 0;

    if(audioObject) {
        uint64_t val = audioObject->get<adm::AudioObjectId>().get<adm::AudioObjectIdValue>().get();
        op += (val << 32);
    }

    if(trackUid) {
        op += trackUid->get<adm::AudioTrackUidId>().get<adm::AudioTrackUidIdValue>().get();;
    }

    return op;
}

MetadataExtractor::MetadataExtractor(Reader * parentReader, std::shared_ptr<adm::Document> parsedDocument) : parentReader{ parentReader }, parsedDocument{ parsedDocument }
{
}

MetadataExtractor::~MetadataExtractor()
{
}

int MetadataExtractor::discoverNewRenderableItems()
{
    if(!parsedDocument) {
        getExceptionHandler()->logException("No parsedDocument!");
        return -1; // -1 = Error
    }

    // Quick check - do we already have the same number of RenderableItemChannels as AudioTrackUids?
    auto audioTrackUids = parsedDocument->getElements<adm::AudioTrackUid>();
    assert(audioTrackUids.size() >= renderableItemChannels.size()); // Can't see why audioTrackUids should ever disappear from the document, even in S-ADM
    if(audioTrackUids.size() <= renderableItemChannels.size()) return 0;

    // Find the new ones!
    int newCount = 0;

    auto audioProgrammes = parsedDocument->getElements<adm::AudioProgramme>();
    for(auto audioProgramme : audioProgrammes) {
        newCount += discoverViaAudioProgramme(audioProgramme);
    }

    auto audioContents = parsedDocument->getElements<adm::AudioContent>(); // May not have parent programme
    for(auto audioContent : audioContents) {
        newCount += discoverViaAudioContent(nullptr, audioContent);
    }

    auto audioObjects = parsedDocument->getElements<adm::AudioObject>(); // May not have parent content
    for(auto audioObject : audioObjects) {
        newCount += discoverViaAudioObject(nullptr, nullptr, std::vector<std::shared_ptr<adm::AudioObject>> {audioObject});
    }

    // Strays - add anyway to prevent constantly running this method trying to discover who they belong to
    for(auto audioTrackUid : audioTrackUids) {
        newCount += discoverFromAudioTrackUid(nullptr, nullptr, std::vector<std::shared_ptr<adm::AudioObject>>{}, audioTrackUid);
    }

    return newCount;
}

bool MetadataExtractor::getNextMetadataBlock(MetadataBlock * metadataBlock)
{
    getExceptionHandler()->clearException(); // Clear because this method can return false without an exception.

    // Quick check if nothing to send;
    if(validRenderableItems.size() == 0) return false;

    // Need to check if this item has more blocks, otherwise move on;
    int checksFinalIndex = idIndexOfLastRenderableItemSent;
    int itemIdIndex = idIndexOfLastRenderableItemSent; // We'll +1 on entering first loop
    std::shared_ptr<RenderableItem> currentItem;
    std::shared_ptr<RenderableItemChannel> currentItemChannel;
    adm::TypeDescriptor currentTypeDefinition = adm::TypeDefinition::UNDEFINED;

    bool nextItemFound{ false };

    do {
        currentItemChannel.reset();

        // Move on to next item to check;
        itemIdIndex++;
        if(itemIdIndex >= validRenderableItems.size()) itemIdIndex = 0;
        currentItem = validRenderableItems[itemIdIndex];

        // Check it for unsent blocks
        if(currentItem->typeDefinition == adm::TypeDefinition::OBJECTS) {
            currentItemChannel = validRenderableItems[itemIdIndex]->renderableItemChannels.begin()->second; // Only single channel expected in this type of item
            auto objectBlocks = currentItemChannel->audioChannelFormat->getElements<adm::AudioBlockFormatObjects>(); // This is probably quite inefficient if it's a copy op - we need a shortcut in libadm
            int objectBlocksCount = objectBlocks.size();
            if(currentItemChannel->lastSentBlockIndex < (objectBlocksCount - 1)) {
                // Unsent blocks waiting on this channel
                auto block = objectBlocks[++currentItemChannel->lastSentBlockIndex]; // Note increment!
                populateTypeSpecificMetadata(metadataBlock, &block, currentItemChannel);
                nextItemFound = true;
            }

        } else if(currentItem->typeDefinition == adm::TypeDefinition::DIRECT_SPEAKERS) {
            currentItemChannel = validRenderableItems[itemIdIndex]->renderableItemChannels.begin()->second; // Only single channel expected in this type of item
            auto dsBlocks = currentItemChannel->audioChannelFormat->getElements<adm::AudioBlockFormatDirectSpeakers>(); // This is probably quite inefficient if it's a copy op - we need a shortcut in libadm
            int dsBlocksCount = dsBlocks.size();
            if(currentItemChannel->lastSentBlockIndex < (dsBlocksCount - 1)) {
                // Unsent blocks waiting on this channel
                auto block = dsBlocks[++currentItemChannel->lastSentBlockIndex]; // Note increment!
                populateTypeSpecificMetadata(metadataBlock, &block, currentItemChannel);
                nextItemFound = true;
            }

        } else if(currentItem->typeDefinition == adm::TypeDefinition::HOA) {

            uint64_t nextEarliestRtime;
            adm::AudioBlockFormatHoa* nextEarliestBlock = nullptr;

            for(auto& renderableItemChannelPair : validRenderableItems[itemIdIndex]->renderableItemChannels) {
                auto hoaBlocks = renderableItemChannelPair.second->audioChannelFormat->getElements<adm::AudioBlockFormatHoa>();
                int hoaBlocksCount = hoaBlocks.size();
                if(renderableItemChannelPair.second->lastSentBlockIndex < (hoaBlocksCount - 1)) {
                    // Unsent blocks waiting on this channel
                    uint64_t nextRtime = 0;
                    if(hoaBlocks[renderableItemChannelPair.second->lastSentBlockIndex + 1].has<adm::Rtime>()) {
                        nextRtime = hoaBlocks[renderableItemChannelPair.second->lastSentBlockIndex + 1].get<adm::Rtime>().get().count();
                    }
                    if(nextEarliestBlock == nullptr || nextRtime < nextEarliestRtime) {
                        nextEarliestRtime = nextRtime;
                        nextEarliestBlock = &hoaBlocks[renderableItemChannelPair.second->lastSentBlockIndex + 1];
                        currentItemChannel = renderableItemChannelPair.second;
                    }
                }
            }

            if(nextEarliestBlock != nullptr) {
                populateHoaSpecificMetadata(metadataBlock, nextEarliestBlock, validRenderableItems[itemIdIndex]); // Also does incrementing of lastSentBlockIndexes
                nextItemFound = true;
            }

        } else if(currentTypeDefinition == adm::TypeDefinition::BINAURAL) {
            // TODO - Binaural, which will work slightly differently.
        }

        if(nextItemFound) {
            // Finalise by doing common parameters and return

            metadataBlock->id = currentItem->selfId;
            metadataBlock->typeDef = currentItem->typeDefinition.get();
            metadataBlock->audioStartTime = currentItem->startTime;
            metadataBlock->audioEndTime = currentItem->endTime;
            metadataBlock->absoluteDistance = currentItemChannel->absoluteDistance;
            metadataBlock->lowPass = currentItemChannel->lowPass;
            metadataBlock->highPass = currentItemChannel->highPass;
            strncpy(metadataBlock->name, currentItem->presentedName.c_str(), sizeof(metadataBlock->name));
            strncpy(metadataBlock->audioPackFormatId, currentItemChannel->audioPackFormatId.c_str(), sizeof(metadataBlock->audioPackFormatId));

            int idCount = 0;
            for(int i = 0; i < currentItem->admTrees.size(); i++) {
                if(currentItem->admTrees[i].audioProgramme) {
                    metadataBlock->audioProgrammeId[idCount] = currentItem->admTrees[i].audioProgrammeId;
                    idCount++;
                }
            }
            metadataBlock->audioProgrammeIdCount = idCount;

            idIndexOfLastRenderableItemSent = itemIdIndex;
            return true;
        }

    } while(itemIdIndex != checksFinalIndex);

    // Didn't return early - no new blocks available
    return false;
}

int MetadataExtractor::discoverViaAudioProgramme(std::shared_ptr<adm::AudioProgramme> audioProgramme) {
    int newCount = 0;
    auto audioContents = audioProgramme->getReferences<adm::AudioContent>();
    for(auto audioContent : audioContents) {
        newCount += discoverViaAudioContent(audioProgramme, audioContent);
    }
    return newCount;
}

int MetadataExtractor::discoverViaAudioContent(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent) {
    int newCount = 0;
    auto audioObjects = audioContent->getReferences<adm::AudioObject>();
    for(auto audioObject : audioObjects) {
        newCount += discoverViaAudioObject(audioProgramme, audioContent, std::vector<std::shared_ptr<adm::AudioObject>>{audioObject});
    }
    return newCount;
}

int MetadataExtractor::discoverViaAudioObject(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent, std::vector<std::shared_ptr<adm::AudioObject>> audioObjectTree) {
    int newCount = 0;
    assert(audioObjectTree.size() > 0); // Must have at least one AudioObject to call this method
    auto nestedAudioObjects = audioObjectTree.back()->getReferences<adm::AudioObject>();
    for(auto nestedAudioObject : nestedAudioObjects) {
        auto newTree = audioObjectTree;
        newTree.push_back(nestedAudioObject);
        newCount += discoverViaAudioObject(audioProgramme, audioContent, newTree);
    }
    auto audioTrackUids = audioObjectTree.back()->getReferences<adm::AudioTrackUid>();
    for(auto audioTrackUid : audioTrackUids) {
        newCount += discoverFromAudioTrackUid(audioProgramme, audioContent, audioObjectTree, audioTrackUid);
    }
    return newCount;
}

int MetadataExtractor::discoverFromAudioTrackUid(std::shared_ptr<adm::AudioProgramme> audioProgramme, std::shared_ptr<adm::AudioContent> audioContent, std::vector<std::shared_ptr<adm::AudioObject>> audioObjectTree, std::shared_ptr<adm::AudioTrackUid> audioTrackUid) {

    RenderableItemChannelId id = generateRenderableItemId(audioObjectTree.size() > 0? audioObjectTree.back() : nullptr, audioTrackUid);
    std::shared_ptr<RenderableItemChannel> renderableItemChannel;
    bool newRenderableItemCreated = false;

    // Firstly check if we already have it!
    auto existing = getValuePointerFromMap(renderableItemChannels, id);
    if(existing) {

        renderableItemChannel = *existing;

    } else {

        // Create it
        renderableItemChannel = std::make_shared<RenderableItemChannel>();
        renderableItemChannel->selfId = id;
        renderableItemChannel->typeDefinition = adm::TypeDefinition::UNDEFINED;
        renderableItemChannel->renderableItem.reset();

        renderableItemChannel->valid = true;
        renderableItemChannel->channelNum = parentReader->getChannelNumFor(audioTrackUid);
        renderableItemChannel->lastSentBlockIndex = -1; // No blocks pulled yet

        renderableItemChannel->audioTrackUid = audioTrackUid;
        renderableItemChannel->audioChannelFormat = nullptr;
        renderableItemChannel->audioStreamFormat = nullptr;
        renderableItemChannel->audioTrackFormat = audioTrackUid->getReference<adm::AudioTrackFormat>();
        renderableItemChannel->audioPackFormatTree = std::vector<std::shared_ptr<adm::AudioPackFormat>>();
        renderableItemChannel->audioPackFormatId = "";

        renderableItemChannel->highPass = NAN;
        renderableItemChannel->lowPass = NAN;
        renderableItemChannel->absoluteDistance = NAN;


        // We need to get to PF/CF to discover TD and work out if we need to create a new RenderableItem...
        //  Do that first because if we reuse existing, we can omit loads of look-ups

        if(renderableItemChannel->audioTrackFormat) {
            renderableItemChannel->audioStreamFormat = renderableItemChannel->audioTrackFormat->getReference<adm::AudioStreamFormat>();
        }

        if(renderableItemChannel->audioStreamFormat) {
            renderableItemChannel->audioChannelFormat = renderableItemChannel->audioStreamFormat->getReference<adm::AudioChannelFormat>();
            if(renderableItemChannel->audioChannelFormat->has<adm::Frequency>()) {
                auto freq = renderableItemChannel->audioChannelFormat->get<adm::Frequency>();
                if(freq.has<adm::LowPass>()) {
                    renderableItemChannel->lowPass = freq.get<adm::LowPass>().get();
                }
                if(freq.has<adm::HighPass>()) {
                    renderableItemChannel->highPass = freq.get<adm::HighPass>().get();
                }
            }
        }

        if(audioObjectTree.size() > 0 && renderableItemChannel->audioChannelFormat) {
            auto pfs = audioObjectTree.back()->getReferences<adm::AudioPackFormat>();
            for(auto pf : pfs) {
                auto res = tracePackFormatTree(pf, renderableItemChannel->audioChannelFormat);
                if(res.has_value() && res->size() > 0) {
                    renderableItemChannel->audioPackFormatTree = *res;
                    break;
                }
            }
        }

        if(renderableItemChannel->audioPackFormatTree.size() > 0) {
            renderableItemChannel->audioPackFormatId = formatId(renderableItemChannel->audioPackFormatTree.back()->get<adm::AudioPackFormatId>());
            if(renderableItemChannel->audioPackFormatTree.back()->has<adm::AbsoluteDistance>()) {
                renderableItemChannel->absoluteDistance = renderableItemChannel->audioPackFormatTree.back()->get<adm::AbsoluteDistance>().get();
            }
            renderableItemChannel->typeDefinition = renderableItemChannel->audioPackFormatTree.back()->get<adm::TypeDescriptor>();
        } else {
            renderableItemChannel->valid = false;
        }

        if(renderableItemChannel->channelNum < 0 || !renderableItemChannel->audioChannelFormat) {
            renderableItemChannel->valid = false;
        }

        // Register new RenderableItemChannel
        setInMap(renderableItemChannels, id, renderableItemChannel);
        newRenderableItemCreated = true;
    }

    // OK... now get existing/create new RenderableItem

    RenderableItemId renderableItemId = 0;
    if(audioObjectTree.size() > 0) {
        if(renderableItemChannel->typeDefinition == adm::TypeDefinition::DIRECT_SPEAKERS || renderableItemChannel->typeDefinition == adm::TypeDefinition::OBJECTS) {
            // Independent channels, therefore always new RenderableItem, ID'd by AudioObject and TrackUID
            renderableItemId = generateRenderableItemId(audioObjectTree.back(), audioTrackUid);

        } else if(renderableItemChannel->typeDefinition == adm::TypeDefinition::HOA) {
            // Grouped channels, ID'd by AudioObject
            renderableItemId = generateRenderableItemId(audioObjectTree.back(), nullptr);

        } else {
            // TODO: We don't support other typedefs at the mo
            renderableItemChannel->valid = false;
        }
    } else {
        // Unowned channels don't render
        renderableItemChannel->valid = false;
    }

    std::shared_ptr<RenderableItem> renderableItem;
    if(renderableItemId != 0) {
        auto existingRenderableItem = getFromMap(renderableItems, renderableItemId);

        if(existingRenderableItem.has_value() && (*existingRenderableItem) != nullptr) {
            renderableItem = *existingRenderableItem;
        } else {
            renderableItem = std::make_shared<RenderableItem>();
            setInMap(renderableItems, renderableItemId, renderableItem);
        }
    }

    if(renderableItem) {

        bool pushAdmTree = false;
        if(renderableItem->admTrees.size() == 0) {
            // new RenderableItem - fill out all info
            pushAdmTree = true;
            renderableItem->selfId = renderableItemId;
            renderableItem->typeDefinition = renderableItemChannel->typeDefinition;
            renderableItem->audioObjectTree = audioObjectTree;
            renderableItem->presentedName = generatePresentedName(audioObjectTree, renderableItemChannel->audioPackFormatTree, renderableItemChannel->audioChannelFormat, renderableItemChannel->typeDefinition);
            renderableItem->startTime = 0.0;
            renderableItem->duration = INFINITY;
            renderableItem->endTime = INFINITY;
            if(audioObjectTree.size() > 0) {
                if(audioObjectTree.back()->has<adm::Start>()) {
                    renderableItem->startTime = audioObjectTree.back()->get<adm::Start>().get().count() / 1000000000.0;
                }
                if(audioObjectTree.back()->has<adm::Duration>()) {
                    renderableItem->duration = audioObjectTree.back()->get<adm::Duration>().get().count() / 1000000000.0;
                    renderableItem->endTime = renderableItem->startTime + renderableItem->duration;
                }
            }

        } else {
            // existing RenderableItem - just add tree if not already present.
            auto isPresent = [audioProgramme, audioContent](ItemAdmTree &existingAdmTree){
                return existingAdmTree.audioProgramme == audioProgramme && existingAdmTree.audioContent == audioContent;
            };
            if(std::find_if(renderableItem->admTrees.begin(), renderableItem->admTrees.end(), isPresent) == renderableItem->admTrees.end()) {
                // Append tree - wasn't already present
                pushAdmTree = true;
            }
        }
        if(pushAdmTree) {
            uint16_t audioProgrammeId = 0;
            if(audioProgramme) {
                audioProgrammeId = static_cast<uint16_t>(std::stoul(adm::formatId(audioProgramme->get<adm::AudioProgrammeId>()).substr(4), nullptr, 16));
            }
            uint16_t audioContentId = 0;
            if(audioContent) {
                uint16_t audioContentId = static_cast<uint16_t>(std::stoul(adm::formatId(audioContent->get<adm::AudioContentId>()).substr(4), nullptr, 16));
            }
            renderableItem->admTrees.push_back({ audioProgramme, audioContent, audioProgrammeId, audioContentId });
        }

        // Tie together RenderableItem <-> RenderableItemChannel
        setInMap(renderableItem->renderableItemChannels, renderableItemChannel->selfId, renderableItemChannel);
        renderableItemChannel->renderableItem = renderableItem;

        bool overallValid = true;
        for(auto renderableItemChannelPair : renderableItem->renderableItemChannels) {
            if(renderableItemChannelPair.second->valid == false) {
                overallValid = false;
                break;
            }
        }

        auto foundIndex = std::find(validRenderableItems.begin(), validRenderableItems.end(), renderableItem);
        bool existInValidList = foundIndex != validRenderableItems.end();

        if(existInValidList && !overallValid) {
            // Remove
            validRenderableItems.erase(foundIndex);
        }

        if(!existInValidList && overallValid) {
            // Add
            validRenderableItems.push_back(renderableItem);
        }

    }

    return newRenderableItemCreated? 1:0;
}

std::optional<std::vector<std::shared_ptr<adm::AudioPackFormat>>> MetadataExtractor::tracePackFormatTree(std::shared_ptr<adm::AudioPackFormat> fromPackFormat, std::shared_ptr<adm::AudioChannelFormat> toChannelFormat, std::vector<std::shared_ptr<adm::AudioPackFormat>> history)
{
    if(std::find(history.begin(), history.end(), fromPackFormat) != history.end()) return std::optional<std::vector<std::shared_ptr<adm::AudioPackFormat>>>();

    history.push_back(fromPackFormat);

    auto cfs = fromPackFormat->getReferences<adm::AudioChannelFormat>();
    for(auto cf : cfs) {
        if(cf == toChannelFormat) {
            return history;
        }
    }

    auto pfs = fromPackFormat->getReferences<adm::AudioPackFormat>();
    for(auto pf : pfs) {
        auto res = tracePackFormatTree(pf, toChannelFormat, history);
        if(res.has_value()) {
            return res;
        }
    }

    return std::optional<std::vector<std::shared_ptr<adm::AudioPackFormat>>>();
}

void MetadataExtractor::populateTypeSpecificMetadata(MetadataBlock * metadataBlock, adm::AudioBlockFormatObjects * audioBlockFormat, std::shared_ptr<RenderableItemChannel> renderableItemChannel)
{
    metadataBlock->channelCount = 1;
    metadataBlock->channelNums[0] = renderableItemChannel->channelNum;

    metadataBlock->rTime = 0.0;
    if(audioBlockFormat->has<adm::Rtime>()) {
        metadataBlock->rTime = audioBlockFormat->get<adm::Rtime>().get().count() / 1000000000.0;
    }
    metadataBlock->duration = INFINITY;
    if(audioBlockFormat->has<adm::Duration>()) {
        metadataBlock->duration = audioBlockFormat->get<adm::Duration>().get().count() / 1000000000.0;
    }
    metadataBlock->jumpPosition = false;
    if(audioBlockFormat->has<adm::JumpPosition>()){
        auto jumpPosition = audioBlockFormat->get<adm::JumpPosition>();
        if(jumpPosition.has<adm::JumpPositionFlag>() && jumpPosition.get<adm::JumpPositionFlag>().get()){
            metadataBlock->jumpPosition = true;
            metadataBlock->interpolationLength = 0.0; // Contradiction in BS.2076. Supposed to default to block duration, but that would not create the instant jump behaviour
            if(jumpPosition.has<adm::InterpolationLength>()) {
                metadataBlock->interpolationLength = jumpPosition.get<adm::InterpolationLength>().get().count() / 1000000000.0;
            }
        }
    }
    if(audioBlockFormat->has<adm::CartesianPosition>())
    {
        metadataBlock->cartesian = true;
        auto position = audioBlockFormat->get<adm::CartesianPosition>();
        metadataBlock->x = position.get<adm::X>().get();
        metadataBlock->y = position.get<adm::Y>().get();
        metadataBlock->z = position.get<adm::Z>().get();
    }
    else if(audioBlockFormat->has<adm::SphericalPosition>())
    {
        metadataBlock->cartesian = false;
        auto position = audioBlockFormat->get<adm::SphericalPosition>();
        metadataBlock->azimuth = position.get<adm::Azimuth>().get();
        metadataBlock->elevation = position.get<adm::Elevation>().get();
        metadataBlock->distance = 1.0;
        if(position.has<adm::Distance>()){
            metadataBlock->distance = position.get<adm::Distance>().get();
        }
    } else {
        assert(false); // Should always have one or the other!
    }
    metadataBlock->width = 0.0;
    if(audioBlockFormat->has<adm::Width>()) {
        metadataBlock->width = audioBlockFormat->get<adm::Width>().get();
    }
    metadataBlock->height = 0.0;
    if(audioBlockFormat->has<adm::Height>()) {
        metadataBlock->height = audioBlockFormat->get<adm::Height>().get();
    }
    metadataBlock->depth = 0.0;
    if(audioBlockFormat->has<adm::Depth>()) {
        metadataBlock->depth = audioBlockFormat->get<adm::Depth>().get();
    }
    metadataBlock->gain = 1.0;
    if(audioBlockFormat->has<adm::Gain>()) {
        metadataBlock->gain = audioBlockFormat->get<adm::Gain>().get();
    }
    metadataBlock->diffuse = 0.0;
    if(audioBlockFormat->has<adm::Diffuse>()) {
        metadataBlock->diffuse = audioBlockFormat->get<adm::Diffuse>().get();
    }
    metadataBlock->divergence = 0.0;
    metadataBlock->divergenceAzimuthRange = 0.0;
    metadataBlock->divergencePositionRange = 0.0;
    if(audioBlockFormat->has<adm::ObjectDivergence>()) {
        auto objectDivergence = audioBlockFormat->get<adm::ObjectDivergence>();
        if(objectDivergence.has<adm::Divergence>()) {
            metadataBlock->divergence = objectDivergence.get<adm::Divergence>().get();
        }
        if(objectDivergence.has<adm::AzimuthRange>()) {
            metadataBlock->divergenceAzimuthRange = objectDivergence.get<adm::AzimuthRange>().get();
        }
        if(objectDivergence.has<adm::PositionRange>()) {
            metadataBlock->divergencePositionRange = objectDivergence.get<adm::PositionRange>().get();
        }
    }
    metadataBlock->channelLock = false;
    if(audioBlockFormat->has<adm::ChannelLock>()){
        auto channelLock = audioBlockFormat->get<adm::ChannelLock>();
        if(channelLock.has<adm::ChannelLockFlag>() && channelLock.get<adm::ChannelLockFlag>().get()){
            metadataBlock->channelLock = true;
            metadataBlock->channelLockMaxDistance = INFINITY;
            if(channelLock.has<adm::MaxDistance>()) {
                metadataBlock->channelLockMaxDistance = channelLock.get<adm::MaxDistance>().get();
            }
        }
    }
    metadataBlock->screenRef = false;
    if(audioBlockFormat->has<adm::ScreenRef>()) {
        metadataBlock->screenRef = audioBlockFormat->get<adm::ScreenRef>().get();
    }
}

void MetadataExtractor::populateTypeSpecificMetadata(MetadataBlock * metadataBlock, adm::AudioBlockFormatDirectSpeakers * audioBlockFormat, std::shared_ptr<RenderableItemChannel> renderableItemChannel)
{
    metadataBlock->channelCount = 1;
    metadataBlock->channelNums[0] = renderableItemChannel->channelNum;

    metadataBlock->rTime = 0.0;
    if(audioBlockFormat->has<adm::Rtime>()) {
        metadataBlock->rTime = audioBlockFormat->get<adm::Rtime>().get().count() / 1000000000.0;
    }
    metadataBlock->duration = INFINITY;
    if(audioBlockFormat->has<adm::Duration>()) {
        metadataBlock->duration = audioBlockFormat->get<adm::Duration>().get().count() / 1000000000.0;
    }
    metadataBlock->gain = 1.0; // Gain is a common block param, but not available for DS in libadm
    if(audioBlockFormat->has<adm::SpeakerPosition>())
    {
        metadataBlock->cartesian = false;
        auto position = audioBlockFormat->get<adm::SpeakerPosition>();
        metadataBlock->azimuth = position.get<adm::Azimuth>().get();
        metadataBlock->elevation = position.get<adm::Elevation>().get();
        metadataBlock->distance = 1.0;
        if(position.has<adm::Distance>()){
            metadataBlock->distance = position.get<adm::Distance>().get();
        }
    } else {
        assert(false); // Should always have!
    }
    metadataBlock->speakerLabel[0] = 0;
    if(audioBlockFormat->has<adm::SpeakerLabels>()) {
        auto speakerLabels = audioBlockFormat->get<adm::SpeakerLabels>();
        assert(speakerLabels.size() <= 1); // Currently only supporting one label due to fully-contained, fixed-size metadata struct.
        if(speakerLabels.size() > 0) {
            strncpy(metadataBlock->speakerLabel, speakerLabels[0].get().c_str(), sizeof(metadataBlock->speakerLabel));
        }
    }
}

void MetadataExtractor::populateHoaSpecificMetadata(MetadataBlock* metadataBlock, adm::AudioBlockFormatHoa* refAudioBlockFormat, std::shared_ptr<RenderableItem> renderableItem)
{
    uint64_t rTimeNs = 0;

    metadataBlock->rTime = 0.0;
    metadataBlock->duration = INFINITY;
    if(refAudioBlockFormat->has<adm::Rtime>()) {
        rTimeNs = refAudioBlockFormat->get<adm::Rtime>().get().count();
        metadataBlock->rTime = rTimeNs / 1000000000.0;
    }

    metadataBlock->channelCount = renderableItem->renderableItemChannels.size();

    metadataBlock->gain = 1.0; // Gain is a common block param, but not available for DS in libadm

    metadataBlock->cartesian = false;
    metadataBlock->x = 0.0;
    metadataBlock->y = 0.0;
    metadataBlock->z = 0.0;
    metadataBlock->azimuth = 0.0;
    metadataBlock->elevation = 0.0;
    metadataBlock->distance = 0.0;

    // Note that for the following parameters, we're essentially assuming the same value in all blocks in all channelformats of this hoa pack

    metadataBlock->screenRef = false;
    if(refAudioBlockFormat->has<adm::ScreenRef>()) {
        metadataBlock->screenRef = refAudioBlockFormat->get<adm::ScreenRef>().get();
    }

    strncpy(metadataBlock->normalisation, "SN3D", 5);
    if(refAudioBlockFormat->has<adm::Normalization>()) {
        strncpy(metadataBlock->normalisation, refAudioBlockFormat->get<adm::Normalization>().get().c_str(), sizeof(metadataBlock->normalisation));
    }

    metadataBlock->nfcRefDist = 0.0;
    if(refAudioBlockFormat->has<adm::NfcRefDist>()) {
        metadataBlock->nfcRefDist = refAudioBlockFormat->get<adm::NfcRefDist>().get();
    }

    // We're going to iterate through the other RenderableItemChannels and get time-relevant blocks so we can build up a common, cumulative block

    int renderableItemChannelIndex = 0;
    uint64_t nextEarliestRtime;
    adm::AudioBlockFormatHoa* nextEarliestBlock = nullptr;

    for(auto& renderableItemChannelPair : renderableItem->renderableItemChannels) {
        int releventBlockIndex = -1;
        auto hoaBlocks = renderableItemChannelPair.second->audioChannelFormat->getElements<adm::AudioBlockFormatHoa>();

        for(int i = std::max(renderableItemChannelPair.second->lastSentBlockIndex, 0); i < hoaBlocks.size(); i++) {
            uint64_t blockRtime = 0;
            if(hoaBlocks[i].has<adm::Rtime>()) {
                blockRtime = hoaBlocks[i].get<adm::Rtime>().get().count();
            }
            if(blockRtime <= rTimeNs) {
                releventBlockIndex = i;
            } else {
                if(nextEarliestBlock == nullptr || blockRtime < nextEarliestRtime) {
                    nextEarliestRtime = blockRtime;
                    nextEarliestBlock = &hoaBlocks[i];
                }
                break;
            }
        }

        if(releventBlockIndex >= 0) { // Acceptable not to have one... metadata for channel may not have started yet

            metadataBlock->channelNums[renderableItemChannelIndex] = renderableItemChannelPair.second->channelNum;
            metadataBlock->order[renderableItemChannelIndex] = 0;
            if(hoaBlocks[releventBlockIndex].has<adm::Order>()) {
                metadataBlock->order[renderableItemChannelIndex] = hoaBlocks[releventBlockIndex].get<adm::Order>().get();
            } else {
                assert(false); //MANDATORY for Hoa Block
            }
            metadataBlock->degree[renderableItemChannelIndex] = 0;
            if(hoaBlocks[releventBlockIndex].has<adm::Degree>()) {
                metadataBlock->degree[renderableItemChannelIndex] = hoaBlocks[releventBlockIndex].get<adm::Degree>().get();
            } else {
                assert(false); //MANDATORY for Hoa Block
            }
            renderableItemChannelIndex++;

            renderableItemChannelPair.second->lastSentBlockIndex = releventBlockIndex;
        }
    }

    // Set duration if we found the next block
    if(nextEarliestBlock != nullptr) {
        assert(rTimeNs <= nextEarliestRtime);
        metadataBlock->duration = (nextEarliestRtime - rTimeNs) / 1000000000.0;
    }

}
