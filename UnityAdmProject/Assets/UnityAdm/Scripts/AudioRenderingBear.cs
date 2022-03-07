using UnityEngine;
using System.Collections.Generic;
using System;
using ADM;

namespace ADM
{
    public enum SrcType
    {
        SincBestQuality,
        SincMediumQuality,
        SincFastest,
        ZeroOrderHold,
        Linear
    };

    public struct ChannelAudioBounds
    {
        public int lowerFrameBound;
        public int upperFrameBound;
    }

    class ItemBlockTracker
    {
        public ItemBlockTracker(UInt64 id)
        {
            itemId = id;
        }

        public UInt64 id
        {
            get => itemId;
        }

        private UInt64 itemId;
        public int blockSendCounter = 0;
    }

    public class BearItemTracker
    {
        // No standard containers really suitable, so make our own

        public void filterByAudioProgrammeId(int progId)
        {
            currentAudioProgrammeIdFilter = progId;
            foreach(ItemBlockTracker item in orderedItems)
            {
                // Jump back one block so BEAR has an initial state for newly contributing items
                if(item.blockSendCounter > 0) item.blockSendCounter--;
            }
            mappingsDirty = true;
        }

        public void removeFilter()
        {
            filterByAudioProgrammeId(-1);
        }

        public void addId(UInt64 id)
        {
            orderedItems.Add(new ItemBlockTracker(id));
            mappingsDirty = true;
        }

        public void addIdIfMissing(UInt64 id)
        {
            if (!hasId(id))
            {
                addId(id);
            }
        }

        public bool hasId(UInt64 id)
        {
            return getIndexOfId(id) >= 0;
        }

        public int countIds()
        {
            return orderedFilteredItems.Count;
        }

        public int countChannels()
        {
            Debug.Assert(!mappingsDirty, "Maps are dirty - ensure updateMappings is called first (will need GlobalState.metadataHandler.renderableItems lock)");
            return inputChannelNums.Length;
        }

        public UInt64 getIdAtIndex(int index)
        {
            return orderedFilteredItems[index].id;
        }

        public int getIndexOfId(UInt64 id)
        {
            for(int i=0; i < orderedFilteredItems.Count; i++)
            {
                if (orderedFilteredItems[i].id == id) return i;
            }
            return -1;
        }

        public int getBlockSendCounterAtIndex(int index)
        {
            return orderedFilteredItems[index].blockSendCounter;
        }

        public int incBlockSendCounterAtIndex(int index)
        {
            orderedFilteredItems[index].blockSendCounter++;
            return orderedFilteredItems[index].blockSendCounter;
        }

        public void updateMappings()
        {
            if (mappingsDirty)
            {
                orderedFilteredItems.Clear();
                for (int i = 0; i < orderedItems.Count; i++)
                {
                    if (currentAudioProgrammeIdFilter < 0 || GlobalState.metadataHandler.renderableItems[orderedItems[i].id].audioProgrammeIds.Contains(currentAudioProgrammeIdFilter))
                    {
                        orderedFilteredItems.Add(orderedItems[i]);
                    }
                }

                int totalChs = 0;
                for (int i = 0; i < orderedFilteredItems.Count; i++)
                {
                    totalChs += GlobalState.metadataHandler.renderableItems[orderedFilteredItems[i].id].originChannelNums.Count;
                }

                inputChannelNums = new int[totalChs];
                audioBounds = new ChannelAudioBounds[totalChs];
                int arrIndex = 0;

                for (int index = 0; index < orderedFilteredItems.Count; index++)
                {
                    for (int ch = 0; ch < GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].originChannelNums.Count; ch++)
                    {
                        inputChannelNums[arrIndex] = GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].originChannelNums[ch];
                        audioBounds[arrIndex] = new ChannelAudioBounds();
                        audioBounds[arrIndex].lowerFrameBound = GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].audioStartFrame;
                        audioBounds[arrIndex].upperFrameBound = GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].audioEndFrame;
                        arrIndex++;
                    }
                    if (DebugSettings.BearChannelAssignments) Debug.Log("BEAR Channel " + index + " assigned to \"" + GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].name + "\"");
                }
                mappingsDirty = false;
            }
        }

        public ref int[] getChannelMap()
        {
            Debug.Assert(!mappingsDirty, "Maps are dirty - ensure updateMappings is called first (will need GlobalState.metadataHandler.renderableItems lock)");
            return ref inputChannelNums;
        }

        public int[] getChannelIndicesForIndex(int index)
        {
            var chs = new int[GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].originChannelNums.Count];
            int startCh = 0;
            for (int i = 0; i < index; i++)
            {
                startCh += GlobalState.metadataHandler.renderableItems[orderedFilteredItems[i].id].originChannelNums.Count;
            }
            for (int i = 0; i < GlobalState.metadataHandler.renderableItems[orderedFilteredItems[index].id].originChannelNums.Count; i++)
            {
                chs[i] = startCh + i;
            }
            return chs;
        }

        public ref ChannelAudioBounds[] getAudioBounds()
        {
            Debug.Assert(!mappingsDirty, "Maps are dirty - ensure updateMappings is called first (will need GlobalState.metadataHandler.renderableItems lock)");
            return ref audioBounds;
        }

        private List<ItemBlockTracker> orderedItems = new List<ItemBlockTracker>();
        private List<ItemBlockTracker> orderedFilteredItems = new List<ItemBlockTracker>();
        private int currentAudioProgrammeIdFilter = -1;
        private bool mappingsDirty = false;
        private int[] inputChannelNums = new int[0];
        private ChannelAudioBounds[] audioBounds = new ChannelAudioBounds[0];
    }

    public class BearAudioRenderer : AudioRenderer
    {
        private AudioSource audioSource;
        private AudioClip audioClip;
        private GameObject gameObject;
        private UnityAdmFilterComponent filter;

        private int clipFrames = 0;
        private int sampleRate = 0;
        public bool internalHaltPlayback = false; // Can't stop from a thread, so use this flag to trigger on next update

        public bool offsetCalculated = false;
        private double scheduledStartTime;
        public int framePosition = 0;
        public int framePositionOffset = 0;
        private bool impulseSent = false;

        public readonly object bearObjectsLock = new object();
        public BearItemTracker bearObjects = new BearItemTracker();
        public readonly object bearDirectSpeakersLock = new object();
        public BearItemTracker bearDirectSpeakers = new BearItemTracker();
        public readonly object bearHoaLock = new object();
        public BearItemTracker bearHoa = new BearItemTracker();

        public BearAudioRenderer()
        {
            if (DebugSettings.ModuleStartups) Debug.Log("Starting BearAudioRenderer...");

            gameObject = new GameObject("BEAR Audio");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            filter = gameObject.AddComponent<UnityAdmFilterComponent>();
            filter.enabled = true;
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.dopplerLevel = 0.0f;
            audioSource.spatialBlend = 0.0f;
            audioSource.loop = false;
            audioSource.playOnAwake = false;

            sampleRate = LibraryInterface.getSampleRate();
            if (sampleRate == 0)
            {
                Debug.LogError("Library reported sample rate as 0");
            }
            clipFrames = LibraryInterface.getNumberOfFrames();
            if (clipFrames == 0)
            {
                Debug.LogError("Library reported frame count as 0");
            }
            if (GlobalState.startingAdmPlayheadPosition < 0)
            {
                clipFrames += Mathf.CeilToInt((float)((-GlobalState.startingAdmPlayheadPosition) * sampleRate));
            }
            if (audioSource.clip)
            {
                audioSource.clip = null;
            }
            audioClip = AudioClip.Create("BEAR", clipFrames, 2, sampleRate, true, OnAudioReadBear, OnAudioSetPositionBear);
            audioSource.clip = audioClip;

            string filepath = GlobalState.BearDataFilePath;
            if (filepath != null && filepath.Trim().Length == 0)
            {
                filepath = PathHelpers.GetApplicationPath() + "/Assets/UnityADM/Data/default.tf";
            }
            if (System.IO.File.Exists(filepath))
            {
                int opBufferFrames, opBufferCount;
                AudioSettings.GetDSPBufferSize(out opBufferFrames, out opBufferCount);
                if (!LibraryInterface.setupBearEx(GlobalState.BearMaxObjectChannels,
                    GlobalState.BearMaxDirectSpeakerChannels,
                    GlobalState.BearMaxHoaChannels,
                    System.Math.Max(System.Math.Max(opBufferFrames * opBufferCount, GlobalState.BearInternalBlockSize * 2), 4096),
                    GlobalState.BearInternalBlockSize,
                    StringHelpers.stringToAsciiBytes(filepath),
                    StringHelpers.stringToAsciiBytes(GlobalState.BearFftImplementation)))
                {
                    Debug.LogError("Setup BEAR: " + LibraryInterface.getLatestExceptionString());
                }
            }
            else
            {
                Debug.LogError("BEAR Data file not found! Please make sure the data file exists in the following location:\n" + filepath);
            }
        }

        public void scheduleAudioPlayback(double forDspTime)
        {
            scheduledStartTime = forDspTime;

            // 2 elements go in to calculating the offset:
            // - Difference between the DSP time when overall playback started and this clips scheduled playback time.
            // - The initial starting playhead position within the ADM.

            double globalLocalPlaybackDiff = scheduledStartTime - GlobalState.dspTimeAtStartOfPlayback;
            // Expect 0 or +ve.
            /// 0 = this was scheduled on initial playback.
            /// +ve = this was scheduled n sec after initial playback (probably discovered later on, so advance internal offset)

            framePositionOffset = (int)((double)sampleRate * (GlobalState.startingAdmPlayheadPosition + globalLocalPlaybackDiff));
            offsetCalculated = true;

            if (DebugSettings.Scheduling)
            {
                Debug.Log("Setting BEAR sample position offset: " + framePositionOffset + " from startingAdmPlayheadPosition: " + GlobalState.startingAdmPlayheadPosition + " + globalLocalPlaybackDiff: " + globalLocalPlaybackDiff);
            }

            audioSource.PlayScheduled(forDspTime);

            if (forDspTime < AudioSettings.dspTime)
            {
                Debug.LogWarning("AudioSource for BEAR missed its scheduled start. Will cause sync issues.\nTry increasing scheduling window.");
            }

        }

        public void stopAudioPlayback()
        {
            offsetCalculated = false;
            if (audioSource) audioSource.Stop();
        }

        public void setAudioProgrammeId(int progId)
        {
            lock (GlobalState.metadataHandler.renderableItemsLock)
            {
                bearObjects.filterByAudioProgrammeId(progId);
                bearObjects.updateMappings(); // Forces a regen if dirty
                bearDirectSpeakers.filterByAudioProgrammeId(progId);
                bearDirectSpeakers.updateMappings(); // Forces a regen if dirty
                bearHoa.filterByAudioProgrammeId(progId);
                bearHoa.updateMappings(); // Forces a regen if dirty
            }
        }

        public void resetAudioProgrammeId()
        {
            lock (GlobalState.metadataHandler.renderableItemsLock)
            {
                bearObjects.removeFilter();
                bearObjects.updateMappings(); // Forces a regen if dirty
                bearDirectSpeakers.removeFilter();
                bearDirectSpeakers.updateMappings(); // Forces a regen if dirty
                bearHoa.removeFilter();
                bearHoa.updateMappings(); // Forces a regen if dirty
            }
        }

        public void configureNewItems(ref List<UInt64> itemsAwaitingConfig)
        {
            // Don't need to do any per-object work here BUT we need to do our channel mappings

            lock (GlobalState.metadataHandler.renderableItemsLock)
            {
                foreach (var id in itemsAwaitingConfig)
                {
                    var typeDef = GlobalState.metadataHandler.renderableItems[id].typeDef;

                    if (typeDef == AdmTypeDefs.OBJECTS)
                    {
                        lock (bearObjectsLock)
                        {
                            {
                                bearObjects.addId(id);
                            }
                        }
                    }
                    else if (typeDef == AdmTypeDefs.DIRECTSPEAKERS)
                    {
                        lock (bearDirectSpeakersLock)
                        {
                            {
                                bearDirectSpeakers.addId(id);
                            }
                        }
                    }
                    else if (typeDef == AdmTypeDefs.HOA)
                    {
                        lock (bearHoaLock)
                        {
                            {
                                bearHoa.addId(id);
                            }
                        }
                    }
                }
                bearObjects.updateMappings(); // Forces a regen if dirty
                bearDirectSpeakers.updateMappings(); // Forces a regen if dirty
                bearHoa.updateMappings(); // Forces a regen if dirty
            }

        }

        public void handleMetadataUpdate(MetadataUpdate metadataUpdate)
        {
            // N/A - process method will pull it's own metadata from MetadataHandler
            // This method can be left blank
        }

        private void OnAudioReadBear(float[] data)
        {
            data[0] = impulseSent ? 0.1f : 1.0f;
            impulseSent = true;
            for (int i = 1; i < data.Length; i++)
            {
                data[i] = 0.1f;
            }
        }

        private void OnAudioSetPositionBear(int newPosition)
        {
            impulseSent = false;
            framePosition = newPosition;
            if (DebugSettings.AudioClipConfig) Debug.Log("Setting BEAR frame position: " + newPosition);
        }

        public void setListener()
        {
            Vector3 position = Camera.main.transform.position;
            Quaternion orientation = Camera.main.transform.rotation;

            //NOTE: Z/Y swapped!! - different coordinate systems
            if (!LibraryInterface.setListener(position.x, position.z, position.y, orientation.w, orientation.x, orientation.z, orientation.y))
            {
                Debug.LogError("BEAR Render: " + LibraryInterface.getLatestExceptionString());
                internalHaltPlayback = true;
                return;
            }

            if (DebugSettings.BearListenerPosition)
            {
                Debug.Log("Position Sent, X: " + position.x + " Y: " + position.y + " Z: " + position.z);
                Debug.Log("Orientation Sent, W:" + orientation.w + " X: " + orientation.x + " Y: " + orientation.z + " Z: " + orientation.y);
            }

            /* Temp: Determine coordinate system
                var vec = LibraryInterface.getLookVec();
                Debug.Log("Current Look,  X: " + vec.x + " Y: " + vec.y + " Z: " + vec.z);
                vec = LibraryInterface.getUpVec();
                Debug.Log("90deg Up,  X: " + vec.x + " Y: " + vec.y + " Z: " + vec.z);
                vec = LibraryInterface.getRightVec();
                Debug.Log("90deg Right,  X: " + vec.x + " Y: " + vec.y + " Z: " + vec.z);
            */
        }

        public void onUpdateBegin()
        {
            if (internalHaltPlayback)
            {
                stopAudioPlayback();
            }
            setListener();
        }

        public void onUpdateEnd()
        {
        }

        public void shutdown()
        {
            if (audioSource || gameObject)
            {
                if (DebugSettings.ModuleStartups) Debug.Log("Killing BearAudioRenderer...");
                if (audioSource) audioSource.Stop();
                if (gameObject) UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}