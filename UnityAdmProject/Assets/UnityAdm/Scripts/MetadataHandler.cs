using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using ADM;

namespace ADM
{
    public enum AdmTypeDefs
    {
        UNDEFINED,
        DIRECTSPEAKERS,
        MATRIX,
        OBJECTS,
        HOA,
        BINAURAL
    }

    public enum MetadataRunState
    {
        UNKNOWN,
        NO_METADATA,
        REACHED_END,
        IN_GAP,
        PROCESSING
    };

    public class RenderableItem
    {
        public List<int> originChannelNums;
        public string name;
        public List<BlockData> blocks; // TODO: Not really blocks... ItemMetadata?
        public int currentBlockIndex;
        public MetadataRunState metadataRunState;
        public AdmTypeDefs typeDef;
        public List<int> audioProgrammeIds;

        public double audioStartTime;
        public double audioEndTime;

        private int _audioStartFrame = 0;
        public int audioStartFrame
        {
            get { return _audioStartFrame; }
        }

        private int _audioEndFrame = int.MaxValue;
        public int audioEndFrame
        {
            get { return _audioEndFrame; }
        }

        public void calculateAudioFrameRange(int refSampleRate)
        {
            double sr = refSampleRate;
            _audioStartFrame = (int)(audioStartTime * sr);
            _audioEndFrame = int.MaxValue;
            if (!double.IsInfinity(audioEndTime))
            {
                _audioEndFrame = (int)(audioEndTime * sr);
            }
        }
    };

    public class BlockData
    {

        public RawMetadataBlock metadataBlockOriginal; // TODO: Try to avoid "block" since that already has a meaning in ADM and these aren't directly mappable to them (esp with combined cfs in an item)
        public RawMetadataBlock metadataBlockProcessed; // Have to make public because we do some pass-by-refs which can't be done via the metadataBlock getter
        private long _metadataBlockProcessedAtRevision = -1;
        public long metadataBlockProcessedRevision
        {
            get { return _metadataBlockProcessedAtRevision; }
        }

        public RawMetadataBlock metadataBlock
        {
            get {
                processMetadataBlockIfNecessary();
                return metadataBlockProcessed;
            }
        }

        public bool metadataBlockRequiresReprocessing()
        {
            return _metadataBlockProcessedAtRevision < GlobalState.propertiesRevisionCounter;
        }

        public bool processMetadataBlockIfNecessary()
        {
            if (!metadataBlockRequiresReprocessing())
            {
                return false;
            }
            processMetadataBlock();
            return true;
        }

        public void processMetadataBlock()
        {
            metadataBlockProcessed = metadataBlockOriginal;
            _metadataBlockProcessedAtRevision = GlobalState.propertiesRevisionCounter;

            if (metadataBlockProcessed.cartesian == 1)
            {
                metadataBlockProcessed.azimuth = 0.0;
                metadataBlockProcessed.elevation = 0.0;
                metadataBlockProcessed.distance = Mathf.Sqrt((float)((metadataBlockProcessed.x * metadataBlockProcessed.x) + (metadataBlockProcessed.y * metadataBlockProcessed.y) + (metadataBlockProcessed.z * metadataBlockProcessed.z)));
                if (metadataBlockProcessed.distance > 0.0)
                {
                    metadataBlockProcessed.azimuth = -Mathf.Atan2((float)(metadataBlockProcessed.x), (float)(metadataBlockProcessed.y)) * 180.0 / Mathf.PI;
                    metadataBlockProcessed.elevation = Mathf.Asin((float)(metadataBlockProcessed.z / metadataBlockProcessed.distance)) * 180.0 / Mathf.PI;
                }

            }

            // Definitely have Sph coords at this point (existing or just computed)
            // Sph offsets
            bool sphOffsetApplied = false;
            if (metadataBlockProcessed.typeDef == (byte)AdmTypeDefs.OBJECTS)
            {
                if (GlobalState.applyObjectsSphOffset)
                {
                    sphOffsetApplied = true;
                    metadataBlockProcessed.azimuth += GlobalState.objectsAzimuthOffset;
                    metadataBlockProcessed.elevation += GlobalState.objectsElevationOffset;
                    metadataBlockProcessed.distance *= GlobalState.objectsDistanceMultiplier;
                }
            }
            else if (metadataBlockProcessed.typeDef == (byte)AdmTypeDefs.DIRECTSPEAKERS)
            {
                if (GlobalState.applyDirectSpeakersSphOffset)
                {
                    sphOffsetApplied = true;
                    metadataBlockProcessed.azimuth += GlobalState.directSpeakersAzimuthOffset;
                    metadataBlockProcessed.elevation += GlobalState.directSpeakersElevationOffset;
                    metadataBlockProcessed.distance *= GlobalState.directSpeakersDistanceMultiplier;
                }
            }

            if (metadataBlockProcessed.cartesian == 0 || sphOffsetApplied) // Either never had cartesian, or carts will need updating due to sph offsetting
            {
                // Update Cartesian
                metadataBlockProcessed.x = metadataBlockProcessed.distance * Mathf.Sin((float)(-metadataBlockProcessed.azimuth * Mathf.PI / 180.0)) * Mathf.Cos((float)(metadataBlockProcessed.elevation * Mathf.PI / 180.0));
                metadataBlockProcessed.y = metadataBlockProcessed.distance * Mathf.Cos((float)(-metadataBlockProcessed.azimuth * Mathf.PI / 180.0)) * Mathf.Cos((float)(metadataBlockProcessed.elevation * Mathf.PI / 180.0));
                metadataBlockProcessed.z = metadataBlockProcessed.distance * Mathf.Sin((float)(metadataBlockProcessed.elevation * Mathf.PI / 180.0));
            }

            // Cart offsets
            bool cartOffsetApplied = false;
            if (metadataBlockProcessed.typeDef == (byte)AdmTypeDefs.OBJECTS)
            {
                if (GlobalState.applyObjectsCartOffset)
                {
                    cartOffsetApplied = true;
                    metadataBlockProcessed.x += GlobalState.objectsXOffset;
                    metadataBlockProcessed.y += GlobalState.objectsYOffset;
                    metadataBlockProcessed.z += GlobalState.objectsZOffset;
                }
            }
            else if (metadataBlockProcessed.typeDef == (byte)AdmTypeDefs.DIRECTSPEAKERS)
            {
                if (GlobalState.applyDirectSpeakersCartOffset)
                {
                    cartOffsetApplied = true;
                    metadataBlockProcessed.x += GlobalState.directSpeakersXOffset;
                    metadataBlockProcessed.y += GlobalState.directSpeakersYOffset;
                    metadataBlockProcessed.z += GlobalState.directSpeakersZOffset;
                }
            }

            if (cartOffsetApplied) // Sph will need updating due to cart offsetting
            {
                metadataBlockProcessed.azimuth = 0.0;
                metadataBlockProcessed.elevation = 0.0;
                metadataBlockProcessed.distance = Mathf.Sqrt((float)((metadataBlockProcessed.x * metadataBlockProcessed.x) + (metadataBlockProcessed.y * metadataBlockProcessed.y) + (metadataBlockProcessed.z * metadataBlockProcessed.z)));
                if (metadataBlockProcessed.distance > 0.0)
                {
                    metadataBlockProcessed.azimuth = -Mathf.Atan2((float)(metadataBlockProcessed.x), (float)(metadataBlockProcessed.y)) * 180.0 / Mathf.PI;
                    metadataBlockProcessed.elevation = Mathf.Asin((float)(metadataBlockProcessed.z / metadataBlockProcessed.distance)) * 180.0 / Mathf.PI;
                }

            }

            //absoluteDistance
            if (GlobalState.alwaysOverrideAbsoluteDistance || double.IsNaN(metadataBlockProcessed.absoluteDistance) || metadataBlockProcessed.absoluteDistance < 0.0)
            {
                metadataBlockProcessed.absoluteDistance = GlobalState.defaultReferenceDistance;
            }

        }

        public double startTime
        {
            get { return metadataBlock.rTime; }
        }

        public double duration
        {
            get { return metadataBlock.duration; }
        }

        private bool _endTimeSet = false;
        private double _endTime;
        public double endTime
        {
            get
            {
                if (!_endTimeSet)
                {
                    _endTimeSet = true;
                    _endTime = metadataBlock.rTime + metadataBlock.duration;
                }
                return _endTime;
            }
        }

        private Vector3 _finalPosInGame;
        private long _finalPosInGameFromRevision = -1;
        public ref Vector3 finalPosInGame()
        {
            long targetRevision = GlobalState.propertiesRevisionCounter;
            if (_finalPosInGameFromRevision < targetRevision)
            {

                // NOTE: Z and Y are swapped!! different coordinate systems;
                if(double.IsNaN(metadataBlock.absoluteDistance))
                {
                    _finalPosInGame.Set(
                        (float)(metadataBlock.x * GlobalState.defaultReferenceDistance),
                        (float)(metadataBlock.z * GlobalState.defaultReferenceDistance),
                        (float)(metadataBlock.y * GlobalState.defaultReferenceDistance));
                }
                else
                {
                    _finalPosInGame.Set(
                        (float)(metadataBlock.x * metadataBlock.absoluteDistance),
                        (float)(metadataBlock.z * metadataBlock.absoluteDistance),
                        (float)(metadataBlock.y * metadataBlock.absoluteDistance));
                }

                _finalPosInGameFromRevision = targetRevision;
            }
            return ref _finalPosInGame;
        }

        public bool moveSpherically
        {
            get
            {
                return metadataBlock.cartesian == 0;
            }
        }

        public bool jumpPosition
        {
            get
            {
                return metadataBlock.jumpPosition > 0;
            }
        }

        public double interpolationLength
        {
            get
            {
                return jumpPosition ? metadataBlock.interpolationLength : metadataBlock.duration;
            }
        }

    }

    public class MetadataUpdate
    {
        public UInt64 forId;
        public AdmTypeDefs typeDef;
        public MetadataRunState metadataRunState;
        public Vector3 inGamePosition;
        public bool audioRunning;
        public float gain;
    }

    public class MetadataHandler
    {
        private RawMetadataBlock latestIncomingMetadata = new RawMetadataBlock { }; // Prevents redeclaring everytime
        private readonly object latestIncomingMetadataLock = new object();

        private MetadataUpdate latestDispatchedMetadata = new MetadataUpdate { }; // Prevents redeclaring everytime
        private readonly object latestDispatchedMetadataLock = new object();

        public Dictionary<UInt64, RenderableItem> renderableItems = new Dictionary<UInt64, RenderableItem>();
        public readonly object renderableItemsLock = new object();

        public List<UInt64> itemsAwaitingCreate = new List<UInt64>();
        public readonly object itemsAwaitingCreateLock = new object();

        private int sampleRate = 0;
        private string currentlyLoadedFile;

        private float lastDispatchedTimeSnapshot = float.NegativeInfinity;

        enum ThreadState
        {
            UNINITIALISED,
            RUNNING,
            DO_STOP,
            STOPPED
        }
        private ThreadState getBlocksThreadState = ThreadState.UNINITIALISED;
        private Thread getBlocksThread;

        public MetadataHandler()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Starting MetadataHandler...");
        }

        public string currentFile
        {
            get { return currentlyLoadedFile; }
        }

        public bool readFile(string filePath)
        {
            if (currentlyLoadedFile != null && currentlyLoadedFile.Trim() != "")
            {
                Debug.LogError("MetadataHandler already has a file loaded!");
                return false;
            }

            float startTime = Time.realtimeSinceStartup;
            if (DebugSettings.Profiling) Debug.Log("Read Starting: " + filePath);
            int res = LibraryInterface.readAdm(StringHelpers.stringToAsciiBytes(filePath));
            if (DebugSettings.Profiling) Debug.Log("Read took (ms): " + ((Time.realtimeSinceStartup - startTime) * 1000.0));
            if (res == 0)
            {
                sampleRate = LibraryInterface.getSampleRate();
                currentlyLoadedFile = filePath;
                //getBlocksInitialPull(); Don't assume this! Other modules (renderers etc) may not be ready yet)
                return true;
            }
            else
            {
                Debug.LogError(LibraryInterface.getLatestExceptionString());
                return false;
            }
        }

        public void reset()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Reseting MetadataHandler...");
            lock (renderableItemsLock)
            {
                lock (itemsAwaitingCreateLock)
                {
                    itemsAwaitingCreate.Clear();
                    foreach (var id in renderableItems.Keys)
                    {
                        itemsAwaitingCreate.Add(id);
                    }
                }
            }
        }

        public void shutdown()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Killing MetadataHandler...");
            // Nothing to do here. Everything gets cleared down when the class instance is destroyed.
        }

        public void startBlockPullThread()
        {
            if (getBlocksThread == null)
            {
                getBlocksThread = new Thread(new ThreadStart(getBlocksThreadLoop));
                getBlocksThreadState = ThreadState.STOPPED;
            }
            if (DebugSettings.PullStatsWorkerThread) Debug.Log("Starting Pull Thread...");
            if (getBlocksThreadState != ThreadState.RUNNING)
            {
                getBlocksThreadState = ThreadState.RUNNING;
                getBlocksThread.Start();
            }
        }
        public void stopBlockPullThread()
        {
            if (DebugSettings.PullStatsWorkerThread) Debug.Log("Stopping Pull Thread...");
            if (getBlocksThread != null)
            {
                getBlocksThreadState = ThreadState.DO_STOP;
                while (getBlocksThreadState == ThreadState.DO_STOP) { }
                if (getBlocksThreadState != ThreadState.STOPPED)
                {
                    Debug.LogWarning("Unexpected thread state - expected stopped.");
                }

                getBlocksThread.Abort();
                getBlocksThread = null;
            }
            if (DebugSettings.PullStatsWorkerThread) Debug.Log("Pull Thread Stopped.");
        }

        private void getBlocksThreadLoop()
        {
            while (getBlocksThreadState != ThreadState.DO_STOP)
            {
                if (GlobalState.threadCyclePeriodMs > 0)
                {
                    // TODO: measure actual time since this ran (calls take time themselves)
                    Thread.Sleep(GlobalState.threadCyclePeriodMs);
                }
                int newItems = LibraryInterface.discoverNewRenderableItems(); // Straight to C++ lib
                if (newItems > 0)
                {
                    if (DebugSettings.PullStatsWorkerThread) Debug.Log("Thread discovered " + newItems + " new renderable items");
                }
                getAnotherBlock();
            }
            getBlocksThreadState = ThreadState.STOPPED;
        }

        public void getBlocksInitialPull()
        {
            float startTime = Time.realtimeSinceStartup;
            if (DebugSettings.Profiling) Debug.Log("Initial Renderable Item Discovery Starting... ");
            int renderableItemCount = LibraryInterface.discoverNewRenderableItems(); // Straight to C++ lib
            if (DebugSettings.Profiling) Debug.Log("Initial Renderable Item Discovery took (ms): " + ((Time.realtimeSinceStartup - startTime) * 1000.0));
            if (DebugSettings.PullStatsInitial) Debug.Log("Initial renderableItemCount: " + renderableItemCount);

            startTime = Time.realtimeSinceStartup;
            if (DebugSettings.Profiling) Debug.Log("Initial Block Pull Starting... ");
            int gotBlockCount = 0;
            while (getAnotherBlock())
            {
                // Keep going while it's still returning true
                gotBlockCount++;
            }
            if (DebugSettings.Profiling) Debug.Log("Initial Block Pull took (ms): " + ((Time.realtimeSinceStartup - startTime) * 1000.0));
            if (DebugSettings.PullStatsInitial) Debug.Log("Initial gotBlockCount: " + gotBlockCount);

            startTime = Time.realtimeSinceStartup;
            if (DebugSettings.Profiling) Debug.Log("Creating initial prepared objects... ");
            createPreparedItems();
            if (DebugSettings.Profiling) Debug.Log("Creating initial prepared objects took (ms): " + ((Time.realtimeSinceStartup - startTime) * 1000.0));
        }

        private bool getAnotherBlock()
        {
            long startTime = System.DateTime.Now.Ticks;
            lock (latestIncomingMetadataLock)
            {

                if (!LibraryInterface.getNextMetadataBlock(ref latestIncomingMetadata)) return false;

                // If here - success! latestMetadataBlock should be populated

                lock (renderableItemsLock)
                {
                    if (!renderableItems.ContainsKey(latestIncomingMetadata.id))
                    {
                        prepareItem(ref latestIncomingMetadata);
                    }
                    for (int i = 0; i < latestIncomingMetadata.audioProgrammeIdCount; i++) {
                        int audioProgrammeId = latestIncomingMetadata.audioProgrammeId[i];
                        if (!renderableItems[latestIncomingMetadata.id].audioProgrammeIds.Contains(audioProgrammeId)){
                            renderableItems[latestIncomingMetadata.id].audioProgrammeIds.Add(audioProgrammeId);
                        }
                    }
                    BlockData block = new BlockData();
                    block.metadataBlockOriginal = latestIncomingMetadata;
                    renderableItems[latestIncomingMetadata.id].blocks.Add(block);
                }

            }
            return true;
        }

        private void prepareItem(ref RawMetadataBlock metadataBlock)
        {
            RenderableItem itemData = new RenderableItem();

            itemData.originChannelNums = new List<int>();
            for (int i = 0; i < metadataBlock.channelCount; i++)
            {
                itemData.originChannelNums.Add(metadataBlock.channelNums[i]);
            }
            itemData.audioProgrammeIds = new List<int>();
            itemData.name = StringHelpers.asciiBytesToString(metadataBlock.name);
            itemData.blocks = new List<BlockData>();
            itemData.currentBlockIndex = 0;
            itemData.typeDef = (AdmTypeDefs)metadataBlock.typeDef;
            itemData.audioStartTime = metadataBlock.audioStartTime;
            itemData.audioEndTime = metadataBlock.audioEndTime;
            itemData.calculateAudioFrameRange(sampleRate);

            renderableItems.Add(metadataBlock.id, itemData);
            lock (itemsAwaitingCreateLock)
            {
                itemsAwaitingCreate.Add(metadataBlock.id);
            }
        }

        public void createPreparedItems()
        {
            lock (itemsAwaitingCreateLock)
            {
                if (itemsAwaitingCreate.Count > 0)
                {
                    if (GlobalState.gameObjectHandler != null) GlobalState.gameObjectHandler.configureNewItems(ref itemsAwaitingCreate);
                    if (GlobalState.audioRenderer != null)
                    {
                        GlobalState.audioRenderer.configureNewItems(ref itemsAwaitingCreate);
                    }
                    else if (GlobalState.audioRendererType != AudioRendererType.NONE)
                    {
                        Debug.LogError("DEVELOPER NOTICE: No AudioRenderer constructed prior to createPreparedObjects being called!");
                    }
                    itemsAwaitingCreate.Clear();
                }
            }

        }

        public void dispatchLatestMetadata()
        {
            float nowTimeSnapshot = (float)GlobalState.getEffectiveAdmPlayheadTimeNow();
            lock (renderableItemsLock)
            {
                bool resetBlockIndex = nowTimeSnapshot < lastDispatchedTimeSnapshot;

                foreach (var id in renderableItems.Keys)
                {
                    if (resetBlockIndex) renderableItems[id].currentBlockIndex = 0;
                    populateLatestMetadataForDispatch(id);
                    if (GlobalState.gameObjectHandler != null) GlobalState.gameObjectHandler.handleMetadataUpdate(latestDispatchedMetadata);
                    if (GlobalState.audioRenderer != null) GlobalState.audioRenderer.handleMetadataUpdate(latestDispatchedMetadata);
                }
            }

            lastDispatchedTimeSnapshot = nowTimeSnapshot;

        }

        private void populateLatestMetadataForDispatch(UInt64 id)
        {
            RenderableItem itemData = renderableItems[id];

            float timeSnapshot = (float)GlobalState.getEffectiveAdmPlayheadTimeNow();
            int lastCompletedBlockIndex = -1; //-1 = none processed.
            int processingBlockIndex = -1; //-1 = none in progress.

            int checkingIndex = itemData.currentBlockIndex;
            if (timeSnapshot >= (float)GlobalState.startingAdmPlayheadPosition)
            {
                while (checkingIndex < itemData.blocks.Count)
                {
                    var blockData = itemData.blocks[checkingIndex];

                    if (blockData.startTime <= timeSnapshot)
                    {
                        itemData.currentBlockIndex = checkingIndex;
                        // This block has started. Has it ended?
                        if (blockData.endTime > timeSnapshot)
                        {
                            // No - in progress
                            processingBlockIndex = checkingIndex;
                            break;
                        }
                        else
                        {
                            // Yes - completed this one
                            lastCompletedBlockIndex = checkingIndex;
                            // Don't break - we need to see if the next block has started
                        }
                    }
                    else
                    {
                        //The current block hasn't started yet.
                        break;
                    }
                    checkingIndex++;
                }
            }

            // Perform behaviour based on results

            lock (latestDispatchedMetadataLock)
            {
                latestDispatchedMetadata.forId = id;
                latestDispatchedMetadata.typeDef = itemData.typeDef;
                latestDispatchedMetadata.audioRunning = (itemData.audioStartTime < timeSnapshot) && (itemData.audioEndTime > timeSnapshot);

                if (lastCompletedBlockIndex == (itemData.blocks.Count - 1))
                {
                    // Final block completed - no more blocks to process
                    // - set it's final resting place, set gain to 0.
                    var blockData = itemData.blocks[lastCompletedBlockIndex];
                    latestDispatchedMetadata.inGamePosition = blockData.finalPosInGame();
                    latestDispatchedMetadata.gain = 0.0f;
                    if (DebugSettings.MetadataRunStates && itemData.metadataRunState != MetadataRunState.REACHED_END)
                    {
                        Debug.Log("Metadata end for \"" + itemData.name + "\" - No more blocks to process.");
                    }
                    latestDispatchedMetadata.metadataRunState = MetadataRunState.REACHED_END;
                }

                else if (processingBlockIndex > -1)
                {
                    // Currently processing a block
                    // - Figure out position and gain
                    var blockData = itemData.blocks[processingBlockIndex];

                    // Find the interpolant (progress in to interpolation ramp)
                    float interpolant = 0;

                    if (blockData.jumpPosition)
                    {
                        if (blockData.startTime + blockData.interpolationLength > timeSnapshot)
                        {
                            interpolant = (timeSnapshot - (float)blockData.startTime) / (float)blockData.interpolationLength;
                        }
                        else
                        {
                            interpolant = 1f;
                        }
                    }
                    else
                    {
                        interpolant = (timeSnapshot - (float)blockData.startTime) / (float)blockData.duration;
                    }

                    Vector3 startingPos = blockData.finalPosInGame(); // Default when no preceeding block is to hold
                    float startingGain = 1.0f;
                    if (processingBlockIndex > 0)
                    {
                        startingPos = itemData.blocks[processingBlockIndex - 1].finalPosInGame();
                        startingGain = (float)itemData.blocks[processingBlockIndex - 1].metadataBlock.gain;
                    }

                    if (blockData.moveSpherically)
                    {
                        latestDispatchedMetadata.inGamePosition = Vector3.Slerp(startingPos, blockData.finalPosInGame(), interpolant);
                    }
                    else
                    {
                        latestDispatchedMetadata.inGamePosition = Vector3.Lerp(startingPos, blockData.finalPosInGame(), interpolant);
                    }

                    float gainDiff = (float)blockData.metadataBlock.gain - startingGain;
                    latestDispatchedMetadata.gain = startingGain + interpolant * gainDiff;

                    if (DebugSettings.MetadataRunStates && itemData.metadataRunState != MetadataRunState.PROCESSING)
                    {
                        Debug.Log("Metadata for \"" + itemData.name + "\" processing");
                    }
                    latestDispatchedMetadata.metadataRunState = MetadataRunState.PROCESSING;

                }

                else if (lastCompletedBlockIndex > -1)
                {
                    // Not currently processing a block, but we have processed one before and there are more to come (we're probably in a gap)
                    // - Ensure set to final position and gain of last completed block
                    var blockData = itemData.blocks[lastCompletedBlockIndex];
                    latestDispatchedMetadata.inGamePosition = blockData.finalPosInGame();
                    latestDispatchedMetadata.gain = (float)blockData.metadataBlock.gain;
                    if (DebugSettings.MetadataRunStates && itemData.metadataRunState != MetadataRunState.IN_GAP)
                    {
                        Debug.Log("Metadata gap for \"" + itemData.name + "\"");
                    }
                    latestDispatchedMetadata.metadataRunState = MetadataRunState.IN_GAP;
                }

                else
                {
                    // No blocks have been processed or are being processed - set gain to 0.
                    latestDispatchedMetadata.inGamePosition.Set(0.0f, 0.0f, 0.0f);
                    latestDispatchedMetadata.gain = 0.0f;
                    if (DebugSettings.MetadataRunStates && itemData.metadataRunState != MetadataRunState.NO_METADATA)
                    {
                        Debug.Log("Metadata end for \"" + itemData.name + "\" - No blocks ever processed.");
                    }
                    latestDispatchedMetadata.metadataRunState = MetadataRunState.NO_METADATA;
                }
            }
        }
    }
}

