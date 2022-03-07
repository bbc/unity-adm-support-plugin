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

    public class BearItemTracker
    {
        // No standard containers really suitable, so make our own

        public void addId(UInt64 id)
        {
            orderedIds.Add(id);
            blockSendCounters.Add(0);
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
            return orderedIds.Contains(id);
        }

        public int countIds()
        {
            return orderedIds.Count;
        }

        public int countChannels()
        {
            Debug.Assert(!mappingsDirty, "Maps are dirty - ensure updateMappings is called first (will need GlobalState.metadataHandler.renderableItems lock)");
            return inputChannelNums.Length;
        }

        public UInt64 getIdAtIndex(int index)
        {
            return orderedIds[index];
        }

        public int getIndexOfId(UInt64 id)
        {
            return orderedIds.IndexOf(id);
        }

        public int getBlockSendCounterAtIndex(int index)
        {
            return blockSendCounters[index];
        }

        public int incBlockSendCounterAtIndex(int index)
        {
            blockSendCounters[index]++;
            return blockSendCounters[index];
        }

        public void updateMappings()
        {
            if (mappingsDirty)
            {
                int totalChs = 0;
                for (int i = 0; i < orderedIds.Count; i++)
                {
                    totalChs += GlobalState.metadataHandler.renderableItems[orderedIds[i]].originChannelNums.Count;
                }

                inputChannelNums = new int[totalChs];
                audioBounds = new ChannelAudioBounds[totalChs];
                int arrIndex = 0;

                for (int index = 0; index < orderedIds.Count; index++)
                {
                    for (int ch = 0; ch < GlobalState.metadataHandler.renderableItems[orderedIds[index]].originChannelNums.Count; ch++)
                    {
                        inputChannelNums[arrIndex] = GlobalState.metadataHandler.renderableItems[orderedIds[index]].originChannelNums[ch];
                        audioBounds[arrIndex] = new ChannelAudioBounds();
                        audioBounds[arrIndex].lowerFrameBound = GlobalState.metadataHandler.renderableItems[orderedIds[index]].audioStartFrame;
                        audioBounds[arrIndex].upperFrameBound = GlobalState.metadataHandler.renderableItems[orderedIds[index]].audioEndFrame;
                        arrIndex++;
                    }
                    if (DebugSettings.BearChannelAssignments) Debug.Log("BEAR Channel " + index + " assigned to \"" + GlobalState.metadataHandler.renderableItems[orderedIds[index]].name + "\"");
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
            var chs = new int[GlobalState.metadataHandler.renderableItems[orderedIds[index]].originChannelNums.Count];
            int startCh = 0;
            for (int i = 0; i < index; i++)
            {
                startCh += GlobalState.metadataHandler.renderableItems[orderedIds[i]].originChannelNums.Count;
            }
            for (int i = 0; i < GlobalState.metadataHandler.renderableItems[orderedIds[index]].originChannelNums.Count; i++)
            {
                chs[i] = startCh + i;
            }
            return chs;
        }

        public ref ChannelAudioBounds[] getAudioBounds()
        {
            Debug.Assert(!mappingsDirty, "Maps are is dirty - ensure updateMappings is called first (will need GlobalState.metadataHandler.renderableItems lock)");
            return ref audioBounds;
        }

        private List<UInt64> orderedIds = new List<UInt64>();
        private List<int> blockSendCounters = new List<int>();
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

            string filepath = PathHelpers.GetApplicationPath() + "/Assets/UnityADM/Data/default.tf";
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
            audioSource.Stop();
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
            if (DebugSettings.ModuleStartups) Debug.Log("Killing BearAudioRenderer...");
            audioSource.Stop();
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}