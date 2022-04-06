using UnityEngine;
using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using ADM;
using UnityEngine.Profiling;

namespace ADM
{
    [RequireComponent(typeof(AudioSource))]
    public class UnityAdmFilterComponent : MonoBehaviour
    {
        private BearAudioRenderer bear;
        private double sampleRateDbl;
        private int sampleRateInt;

        private class ProcTiming
        {
            public List<double> actualTimes = new List<double>();
            public List<double> maximumTimes = new List<double>();
        }

        private ProcTiming[] timings = new ProcTiming[] { new ProcTiming(), new ProcTiming() };
        private int activeTimingResults = 0;
        private int minTimingResults = 10;

        private void checkTimings()
        {
            if (timings[activeTimingResults].actualTimes.Count >= minTimingResults) {
                int lastResults = activeTimingResults;
                activeTimingResults = lastResults == 0 ? 1 : 0;

                double actualSum = 0;
                foreach (double val in timings[lastResults].actualTimes) actualSum += val;
                double maximumSum = 0;
                foreach (double val in timings[lastResults].maximumTimes) maximumSum += val;
                if (actualSum > maximumSum)
                {
                    Debug.LogWarning("BEAR Rendering taking too long!");
                }
                timings[lastResults].maximumTimes.Clear();
                timings[lastResults].actualTimes.Clear();
            }
        }

        private void Update()
        {
            checkTimings();
        }

        enum Signal
        {
            TO_START,
            TO_END,
            NONE
        }
        private Signal searchingFor = Signal.TO_START;

        void Start()
        {
            bear = GlobalState.audioRenderer as BearAudioRenderer;
            if (bear == null)
            {
                Debug.LogWarning("UnityAdmFilterComponent expected to find a BearAudioRenderer instance!");
            }
            else
            {
                LibraryInterface.setBearOutputGain(GlobalState.BearOutputGain);
            }

            sampleRateDbl = AudioSettings.outputSampleRate;
            sampleRateInt = AudioSettings.outputSampleRate;


            if (Stopwatch.IsHighResolution)
            {
                Debug.Log("Operations timed using the system's high-resolution performance counter.");
            }
            else
            {
                Debug.Log("Operations timed using the DateTime class.");
            }

            long frequency = Stopwatch.Frequency;
            Debug.Log("  Timer frequency in ticks per second = " + frequency);
            long nanosecPerTick = (1000L * 1000L * 1000L) / frequency;
            Debug.Log("  Timer is accurate within " + nanosecPerTick + " nanoseconds");
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            Stopwatch timer = Stopwatch.StartNew();

            // Useful local func
            bool frameIsNonZero(int frameNum)
            {
                int framePos = frameNum * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    if (data[framePos + channel] != 0.0f)
                    {
                        return true;
                    }
                }
                return false;
            }

            // This makes the assumption that there are no breaks in the signal
            // (i.e, we're always requesting continuous audio)
            // If we want to pause/stop/seek, we should be restarting the AudioRenderer anyway.

            int totalFrames = data.Length / channels;
            double totalFramesTime = totalFrames / sampleRateDbl;

            if (searchingFor == Signal.NONE)
            {
                return;
            }

            int dataWriteStartFrame = 0;
            if (searchingFor == Signal.TO_START)
            {
                while (dataWriteStartFrame < totalFrames)
                {
                    if (frameIsNonZero(dataWriteStartFrame))
                    {
                        break;
                    }
                    dataWriteStartFrame++;
                }
                if (dataWriteStartFrame == totalFrames)
                {
                    return;
                }
                searchingFor = Signal.TO_END;
                if (DebugSettings.BearFilterProcess)
                {
                    Debug.Log("Filter Component; START Triggered (frame " + (dataWriteStartFrame + 1) + "/" + totalFrames + " of block)");
                }
            }

            int dataWriteEndFrame = totalFrames;
            if (searchingFor == Signal.TO_END)
            {
                while (dataWriteEndFrame > dataWriteStartFrame)
                {
                    if (frameIsNonZero(dataWriteEndFrame - 1))
                    {
                        break;
                    }
                    dataWriteEndFrame--;
                }
                if (dataWriteEndFrame < totalFrames)
                {
                    searchingFor = Signal.NONE;
                    if (DebugSettings.BearFilterProcess)
                    {
                        Debug.Log("Filter Component; STOP Triggered (frame " + (dataWriteEndFrame + 1) + "/" + totalFrames + " of block)");
                    }
                }
            }

            int dataWriteFrameCount = dataWriteEndFrame - dataWriteStartFrame;

            if (dataWriteFrameCount == 0)
            {
                return;
            }

            Profiler.BeginSample("prewarnBearRenderSrc");
            if (!LibraryInterface.prewarnBearRenderSrc(bear.framePosition + bear.framePositionOffset, dataWriteFrameCount, sampleRateInt, (int)GlobalState.BearSrcType))
            {
                Debug.LogError("BEAR Prewarn: " + LibraryInterface.getLatestExceptionString());
                bear.internalHaltPlayback = true;
                return;
            }
            Profiler.EndSample();

            // Pump in metadata
            lock (GlobalState.metadataHandler.renderableItemsLock)
            {
                lock (bear.bearObjectsLock)
                {
                    for (int index = 0; index < bear.bearObjects.countIds(); index++)
                    {
                        UInt64 id = bear.bearObjects.getIdAtIndex(index);
                        int blockNum = bear.bearObjects.getBlockSendCounterAtIndex(index);
                        int blockNumLimit = GlobalState.metadataHandler.renderableItems[id].blocks.Count;

                        if (blockNum > 0)
                        {
                            // Ensure last block we sent has not gone stale in meantime
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].processMetadataBlockIfNecessary();
                            long targetRevision = GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessedRevision;
                            if (bear.bearObjects.getLastSentBlockRevisionForIndex(index) < targetRevision)
                            {
                                // Last sent block has since changed (due to script properties change) - resend
                                LibraryInterface.addBearObjectMetadata(index, ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessed);
                                bear.bearObjects.setLastSentBlockRevisionForIndex(index, targetRevision);
                            }
                        }

                        while (blockNum < blockNumLimit)
                        {
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].processMetadataBlockIfNecessary(); // Because we are accessing by ref, we can't use getter to ensure up to date.
                            if (!LibraryInterface.addBearObjectMetadata(index, ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessed))
                            {
                                break;
                            }
                            bear.bearObjects.setLastSentBlockRevisionForIndex(index, GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessedRevision);
                            blockNum = bear.bearObjects.incBlockSendCounterAtIndex(index);
                        }
                    }
                }

                lock (bear.bearDirectSpeakersLock)
                {
                    for (int index = 0; index < bear.bearDirectSpeakers.countIds(); index++)
                    {
                        UInt64 id = bear.bearDirectSpeakers.getIdAtIndex(index);
                        int blockNum = bear.bearDirectSpeakers.getBlockSendCounterAtIndex(index);
                        int blockNumLimit = GlobalState.metadataHandler.renderableItems[id].blocks.Count;

                        if (blockNum > 0)
                        {
                            // Ensure last block we sent has not gone stale in meantime
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].processMetadataBlockIfNecessary();
                            long targetRevision = GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessedRevision;
                            if (bear.bearDirectSpeakers.getLastSentBlockRevisionForIndex(index) < targetRevision)
                            {
                                // Last sent block has since changed (due to script properties change) - resend
                                LibraryInterface.addBearObjectMetadata(index, ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessed);
                                bear.bearDirectSpeakers.setLastSentBlockRevisionForIndex(index, targetRevision);
                            }
                        }

                        while (blockNum < blockNumLimit)
                        {
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].processMetadataBlockIfNecessary(); // Because we are accessing by ref, we can't use getter to ensure up to date.
                            if (!LibraryInterface.addBearDirectSpeakersMetadata(index, ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessed))
                            {
                                break;
                            }
                            bear.bearDirectSpeakers.setLastSentBlockRevisionForIndex(index, GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessedRevision);
                            blockNum = bear.bearDirectSpeakers.incBlockSendCounterAtIndex(index);
                        }
                    }
                }

                lock (bear.bearHoaLock)
                {
                    for (int index = 0; index < bear.bearHoa.countIds(); index++)
                    {
                        UInt64 id = bear.bearHoa.getIdAtIndex(index);
                        int blockNum = bear.bearHoa.getBlockSendCounterAtIndex(index);
                        int blockNumLimit = GlobalState.metadataHandler.renderableItems[id].blocks.Count;

                        if (blockNum > 0)
                        {
                            // Ensure last block we sent has not gone stale in meantime
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].processMetadataBlockIfNecessary();
                            long targetRevision = GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessedRevision;
                            if (bear.bearHoa.getLastSentBlockRevisionForIndex(index) < targetRevision)
                            {
                                // Last sent block has since changed (due to script properties change) - resend
                                LibraryInterface.addBearObjectMetadata(index, ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum - 1].metadataBlockProcessed);
                                bear.bearHoa.setLastSentBlockRevisionForIndex(index, targetRevision);
                            }
                        }

                        while (blockNum < blockNumLimit)
                        {
                            GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].processMetadataBlockIfNecessary(); // Because we are accessing by ref, we can't use getter to ensure up to date.
                            if (!LibraryInterface.addBearHoaMetadata(bear.bearHoa.getChannelIndicesForIndex(index), ref GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessed))
                            {
                                break;
                            }
                            bear.bearHoa.setLastSentBlockRevisionForIndex(index, GlobalState.metadataHandler.renderableItems[id].blocks[blockNum].metadataBlockProcessedRevision);
                            blockNum = bear.bearHoa.incBlockSendCounterAtIndex(index);
                        }
                    }
                }
            }

            Profiler.BeginSample("getBearRenderBounded");
            bool renderRes = LibraryInterface.getBearRenderBounded(
                bear.bearObjects.getChannelMap(),
                bear.bearObjects.getAudioBounds(),
                bear.bearObjects.countIds(),
                bear.bearDirectSpeakers.getChannelMap(),
                bear.bearDirectSpeakers.getAudioBounds(),
                bear.bearDirectSpeakers.countIds(),
                bear.bearHoa.getChannelMap(),
                bear.bearHoa.getAudioBounds(),
                bear.bearHoa.countChannels(),
                data, dataWriteStartFrame, !DebugSettings.BearAudibleStartTick);
            Profiler.EndSample();

            if (!renderRes)
            {
                Debug.LogWarning("BEAR Render: " + LibraryInterface.getLatestExceptionString());
            }

            bear.framePosition += dataWriteFrameCount;

            timer.Stop();
            timings[activeTimingResults].maximumTimes.Add(totalFramesTime);
            timings[activeTimingResults].actualTimes.Add(timer.Elapsed.TotalSeconds);

        }
    }
}