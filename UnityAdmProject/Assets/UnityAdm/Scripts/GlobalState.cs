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
        public static bool alwaysOverrideAbsoluteDistance;
        public static AudioRendererType audioRendererType = AudioRendererType.NONE;
        public static bool renderOnlySelectedAudioProgramme = false;
        public static int selectedAudioProgrammeId = 0;

        // BEAR Specific config
        public static string BearDataFilePath = "";
        public static int BearMaxObjectChannels = 32;
        public static int BearMaxDirectSpeakerChannels = 24;
        public static int BearMaxHoaChannels = 0;
        public static string BearFftImplementation = "";
        public static int BearInternalBlockSize = 1024;
        public static float BearOutputGain = 0.775f;
        public static SrcType BearSrcType = SrcType.SincMediumQuality;

        // Runtime-modifiable Properties
        public static long propertiesRevisionCounter = 0;

        /// Offsetting
        private static double _directSpeakersXOffset = 0.0;
        public static double directSpeakersXOffset
        {
            get { return _directSpeakersXOffset; }
            set
            {
                _directSpeakersXOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _directSpeakersYOffset = 0.0;
        public static double directSpeakersYOffset
        {
            get { return _directSpeakersYOffset; }
            set
            {
                _directSpeakersYOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _directSpeakersZOffset = 0.0;
        public static double directSpeakersZOffset
        {
            get { return _directSpeakersZOffset; }
            set
            {
                _directSpeakersZOffset = value;
                propertiesRevisionCounter++;
            }
        }
        public static bool applyDirectSpeakersCartOffset
        {
            get { return (directSpeakersXOffset != 0.0 || directSpeakersYOffset != 0.0 || directSpeakersZOffset != 0.0); }
        }
        private static double _directSpeakersAzimuthOffset = 0.0;
        public static double directSpeakersAzimuthOffset
        {
            get { return _directSpeakersAzimuthOffset; }
            set
            {
                _directSpeakersAzimuthOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _directSpeakersElevationOffset = 0.0;
        public static double directSpeakersElevationOffset
        {
            get { return _directSpeakersElevationOffset; }
            set
            {
                _directSpeakersElevationOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _directSpeakersDistanceMultiplier = 1.0;
        public static double directSpeakersDistanceMultiplier
        {
            get { return _directSpeakersDistanceMultiplier; }
            set
            {
                _directSpeakersDistanceMultiplier = value;
                propertiesRevisionCounter++;
            }
        }
        public static bool applyDirectSpeakersSphOffset
        {
            get { return (directSpeakersAzimuthOffset != 0.0 || directSpeakersElevationOffset != 0.0 || directSpeakersDistanceMultiplier != 1.0); }
        }

        private static double _objectsXOffset = 0.0;
        public static double objectsXOffset
        {
            get { return _objectsXOffset; }
            set
            {
                _objectsXOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _objectsYOffset = 0.0;
        public static double objectsYOffset
        {
            get { return _objectsYOffset; }
            set
            {
                _objectsYOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _objectsZOffset = 0.0;
        public static double objectsZOffset
        {
            get { return _objectsZOffset; }
            set
            {
                _objectsZOffset = value;
                propertiesRevisionCounter++;
            }
        }
        public static bool applyObjectsCartOffset
        {
            get { return (objectsXOffset != 0.0 || objectsYOffset != 0.0 || objectsZOffset != 0.0); }
        }
        private static double _objectsAzimuthOffset = 0.0;
        public static double objectsAzimuthOffset
        {
            get { return _objectsAzimuthOffset; }
            set
            {
                _objectsAzimuthOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _objectsElevationOffset = 0.0;
        public static double objectsElevationOffset
        {
            get { return _objectsElevationOffset; }
            set
            {
                _objectsElevationOffset = value;
                propertiesRevisionCounter++;
            }
        }
        private static double _objectsDistanceMultiplier = 1.0;
        public static double objectsDistanceMultiplier
        {
            get { return _objectsDistanceMultiplier; }
            set
            {
                _objectsDistanceMultiplier = value;
                propertiesRevisionCounter++;
            }
        }
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