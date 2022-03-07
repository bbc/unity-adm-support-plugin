using UnityEngine;
using ADM;

namespace ADM
{
    public enum PlaybackState
    {
        _UNINITIALISED,
        SCHEDULED,
        STARTED,
        STOPPED
    }

    public static class GlobalState
    {
        // Playback settings
        public static double startingAdmPlayheadPosition = 0.0;
        public static double schedulingWindow = 0.0;

        private static double _dspTimeAtStartOfPlayback = -0.1; // Negative = stopped
        public static double dspTimeAtStartOfPlayback
        {
            get { return _dspTimeAtStartOfPlayback; }
        }

        public static void setPlaybackScheduled(double forTime)
        {
            _dspTimeAtStartOfPlayback = forTime;
        }

        public static void setPlaybackStopped()
        {
            _dspTimeAtStartOfPlayback = -0.1; // Negative = stopped;
        }

        public static PlaybackState playbackState
        {
            get
            {
                if (_dspTimeAtStartOfPlayback < 0.0)
                {
                    return PlaybackState.STOPPED;
                }
                else if (_dspTimeAtStartOfPlayback < AudioSettings.dspTime)
                {
                    return PlaybackState.STARTED;
                }
                else
                {
                    return PlaybackState.SCHEDULED;
                }
            }
        }

        public static bool playing
        {
            // Playback is scheduled or playing
            get { return (playbackState == PlaybackState.SCHEDULED || playbackState == PlaybackState.STARTED); }
        }

        public static double getEffectiveAdmPlayheadTimeNow()
        {
            return AudioSettings.dspTime - dspTimeAtStartOfPlayback + startingAdmPlayheadPosition;
        }

        // Threading
        public static bool runThread = false;
        public static int threadCyclePeriodMs = 0;

        // Rendering config (visual and audio)
        public static bool createIndividualGameObjects()
        {
            if (useVisualisations) return true;
            if (audioRendererType == AudioRendererType.UNITY) return true;
            return false;
        }
        public static bool useVisualisations;
        public static GameObject objectVisualisation;
        public static GameObject dsVisualisation;
        public static GameObject hoaVisualisation;
        public static float defaultReferenceDistance;
        public static AudioRendererType audioRendererType = AudioRendererType.NONE;

        // BEAR Specific config
        public static string BearDataFilePath = "";
        public static int BearMaxObjectChannels = 32;
        public static int BearMaxDirectSpeakerChannels = 24;
        public static int BearMaxHoaChannels = 0;
        public static string BearFftImplementation = "";
        public static int BearInternalBlockSize = 1024;
        public static float BearOutputGain = 0.775f;
        public static SrcType BearSrcType = SrcType.SincMediumQuality;

        // Offsetting
        public static double directSpeakersXOffset = 0.0;
        public static double directSpeakersYOffset = 0.0;
        public static double directSpeakersZOffset = 0.0;
        public static bool applyDirectSpeakersCartOffset
        {
            get { return (directSpeakersXOffset != 0.0 || directSpeakersYOffset != 0.0 || directSpeakersZOffset != 0.0); }
        }
        public static double directSpeakersAzimuthOffset = 0.0;
        public static double directSpeakersElevationOffset = 0.0;
        public static double directSpeakersDistanceMultiplier = 1.0;
        public static bool applyDirectSpeakersSphOffset
        {
            get { return (directSpeakersAzimuthOffset != 0.0 || directSpeakersElevationOffset != 0.0 || directSpeakersDistanceMultiplier != 1.0); }
        }
        public static double objectsXOffset = 0.0;
        public static double objectsYOffset = 0.0;
        public static double objectsZOffset = 0.0;
        public static bool applyObjectsCartOffset
        {
            get { return (objectsXOffset != 0.0 || objectsYOffset != 0.0 || objectsZOffset != 0.0); }
        }
        public static double objectsAzimuthOffset = 0.0;
        public static double objectsElevationOffset = 0.0;
        public static double objectsDistanceMultiplier = 1.0;
        public static bool applyObjectsSphOffset
        {
            get { return (objectsAzimuthOffset != 0.0 || objectsElevationOffset != 0.0 || objectsDistanceMultiplier != 1.0); }
        }

        // Constructed Classes
        public static MetadataHandler metadataHandler = null;
        public static GameObjectHandler gameObjectHandler = null;
        public static AudioRenderer audioRenderer = null;
    }
}