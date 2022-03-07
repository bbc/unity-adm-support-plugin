using System.Collections.Generic;
using UnityEngine;
using System;
using ADM;

namespace ADM
{
    public class UnityAudioRenderer : AudioRenderer
{
    public Dictionary<UInt64, UnityObjectChannelRender> channelRenderers = new Dictionary<UInt64, UnityObjectChannelRender>();
    public readonly object channelRenderersLock = new object();

    public UnityAudioRenderer()
    {
        if (DebugSettings.ModuleStartups) Debug.Log("Starting UnityAudioRenderer...");
    }

    public void scheduleAudioPlayback(double forDspTime)
    {
        lock (channelRenderersLock)
        {
            foreach (var channelRenderer in channelRenderers.Values)
            {
                channelRenderer.scheduleAudioPlayback(forDspTime);
                if (forDspTime < AudioSettings.dspTime)
                {
                    Debug.LogWarning("AudioSource for \"" + channelRenderer.name + "\" missed its scheduled start. Will cause sync issues.");
                }
            }
        }
    }

    public void stopAudioPlayback()
    {
        lock (channelRenderersLock)
        {
            foreach (var channelRenderer in channelRenderers.Values)
            {
                channelRenderer.stopAudioPlayback();
            }
        }
    }

    public void configureNewItems(ref List<UInt64> itemsAwaitingConfig)
    {
        if(GlobalState.gameObjectHandler == null)
        {
            Debug.LogWarning("UnityAudioRenderer: No GameObjectHandler to attach AudioSources to!");
            return;
        }

        lock (GlobalState.metadataHandler.renderableItemsLock)
        {
            lock (channelRenderersLock)
            {
                foreach (var id in itemsAwaitingConfig)
                {
                    var typeDef = GlobalState.metadataHandler.renderableItems[id].typeDef;

                    if (typeDef == AdmTypeDefs.OBJECTS || typeDef == AdmTypeDefs.DIRECTSPEAKERS)
                    {
                        lock (GlobalState.gameObjectHandler.gameObjectsLock)
                        {
                            channelRenderers.Add(id, new UnityObjectChannelRender(
                                GlobalState.metadataHandler.renderableItems[id].name,
                                GlobalState.metadataHandler.renderableItems[id].originChannelNums[0], // We only expect one channel per item for DS/Obj
                                GlobalState.metadataHandler.renderableItems[id].audioStartFrame,
                                GlobalState.metadataHandler.renderableItems[id].audioEndFrame,
                                GlobalState.gameObjectHandler.gameObjects[id].gameObject)
                            );
                        }

                        // If we're already playing, we need to schedule this AudioSource
                        if (GlobalState.playing)
                        {
                            channelRenderers[id].scheduleAudioPlayback(AudioSettings.dspTime + GlobalState.schedulingWindow);
                        }
                    }
                }
            }
        }
    }

    public void handleMetadataUpdate(MetadataUpdate metadataUpdate)
    {
        // UnityObjectChannelRender only interested in gain value
        lock (channelRenderersLock)
        {
            if (channelRenderers.ContainsKey(metadataUpdate.forId))
            {
                channelRenderers[metadataUpdate.forId].setGain(metadataUpdate.gain);
            }
        }
    }

    public void onUpdateBegin()
    {
        lock (channelRenderersLock)
        {
            foreach (var channelRenderer in channelRenderers.Values)
            {
                if(channelRenderer.internalHaltPlayback) channelRenderer.stopAudioPlayback();
            }
        }
    }

    public void onUpdateEnd()
    {
    }

    public void shutdown()
    {
        if (DebugSettings.ModuleStartups) Debug.Log("Killing UnityAudioRenderer...");

        lock (channelRenderersLock)
        {
            foreach (var channelRenderer in channelRenderers.Values)
            {
                channelRenderer.shutdown();
            }
        }
    }

}

    public class UnityObjectChannelRender
    {
        public string name;
        public int[] channelNums;
        private int sampleRate = 0;
        private int availableAudioFrames = 0;
        private int clipFrames = 0; // May be greater than available audio frames if the starting playhead position is negative (delayed start)
        public bool internalHaltPlayback = false; // Can't stop from a thread, so use this flag to trigger on next update

        private GameObject gameObject;
        private AudioSource audioSource;

        private bool offsetCalculated = false;
        private double scheduledStartTime;
        private int framePosition = 0;
        private int framePositionOffset = 0;

        private int lowerFrameBound = 0;
        private int upperFrameBound = int.MaxValue;

        public UnityObjectChannelRender(string desiredName, int originChannelNum, int audioLowerFrameBound, int audioUpperFrameBound, GameObject parentGameObject)
        {
            name = desiredName;
            channelNums = new int[] { originChannelNum };
            lowerFrameBound = audioLowerFrameBound;
            upperFrameBound = audioUpperFrameBound;

            gameObject = parentGameObject;
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.dopplerLevel = 0.0f;
            audioSource.spatialBlend = 1.0f;
            audioSource.loop = false;
            audioSource.playOnAwake = false;

            sampleRate = LibraryInterface.getSampleRate();
            if (sampleRate == 0)
            {
                Debug.LogError("Library reported sample rate as 0");
            }
            availableAudioFrames = LibraryInterface.getNumberOfFrames();
            if (availableAudioFrames == 0)
            {
                Debug.LogError("Library reported frame count as 0");
            }
        }

        public void setGain(float gain)
        {
            audioSource.volume = gain;
        }

        public void scheduleAudioPlayback(double forDspTime)
        {
            audioSource.time = 0.0f; // We do more accurate playback timing ourselves.
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
                Debug.Log("Setting \"" + name + "\" sample position offset: " + framePositionOffset + " from startingAdmPlayheadPosition: " + GlobalState.startingAdmPlayheadPosition + " + globalLocalPlaybackDiff: " + globalLocalPlaybackDiff);
            }

            // Only create audio clip now, otherwise it tries to preload before it knows its offset!
            if (audioSource.clip)
            {
                audioSource.clip = null;
            }
            audioSource.clip = createAudioClip();

            audioSource.PlayScheduled(forDspTime);
        }

        public void stopAudioPlayback()
        {
            audioSource.Stop();
        }

        public void shutdown()
        {
            audioSource.Stop();
            UnityEngine.Object.Destroy(audioSource);
        }

        private AudioClip createAudioClip()
        {
            clipFrames = availableAudioFrames;
            if (GlobalState.startingAdmPlayheadPosition < 0)
            {
                clipFrames += Mathf.CeilToInt((float)((-GlobalState.startingAdmPlayheadPosition) * sampleRate));
            }
            AudioClip clip = AudioClip.Create(name, clipFrames, channelNums.Length, sampleRate, true, OnAudioRead, OnAudioSetPosition);
            if (DebugSettings.AudioClipConfig) Debug.Log("Creating \"" + name + "\" AudioClip from channel num: " + channelNums[0]);
            return clip;
        }

        unsafe void OnAudioRead(float[] data)
        {
            int numFramesToRequest = data.Length / channelNums.Length;

            if (!offsetCalculated)
            {
                Debug.LogWarning("OnAudioRead for \"" + name + "\" called before setting offset!");
            }

            if (!LibraryInterface.getAudioBlockBounded(framePosition + framePositionOffset, numFramesToRequest, channelNums, channelNums.Length, lowerFrameBound, upperFrameBound, data))
            {
                Debug.LogError("Get Audio Block: " + LibraryInterface.getLatestExceptionString());
                internalHaltPlayback = true;
                return;
            }

            framePosition += numFramesToRequest;
        }

        void OnAudioSetPosition(int newPosition)
        {
            framePosition = newPosition;
            if (DebugSettings.AudioClipConfig) Debug.Log("Setting \"" + name + "\" frame position: " + newPosition);
        }
    }
}
