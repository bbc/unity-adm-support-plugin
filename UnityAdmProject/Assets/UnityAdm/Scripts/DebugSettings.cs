using UnityEngine;
using ADM;

namespace ADM
{
    public static class DebugSettings
{
    public static bool AdmSourceSetting = false; // Deducing source file/stream to be loaded
    public static bool Playback = true; // Overall playback state
    public static bool Scheduling = false; // Audio scheduling and synchronisation
    public static bool AudioClipConfig = false; // Creation and configuration of AudioClips and callbacks
    public static bool Profiling = false; // Timing of key functions
    public static bool PullStatsInitial = false; // Renderable items found and blocks pulled in initial pull
    public static bool PullStatsWorkerThread = false; // New Renderable items found in worker thread
    public static bool MetadataRunStates = false; // Per-object metadata processing states
    public static bool ModuleStartups = false; // Which modules (handlers and renderer) are constructed
    public static bool BearChannelAssignments = false; // List which channels are assigned to specific objects
    public static bool BearListenerPosition = false; // Listener position and orientation data sent to BEAR
    public static bool BearFilterProcess = false; // Audio processing in the UnityAdmFilterComponent used for BEAR
    public static bool BearAudibleStartTick = false; // Mix AudioSource audio with BEAR render to hear the start trigger impulse

}

    public static class DebugPlaybackTracker
    {
        private static PlaybackState lastPlaybackMessage = PlaybackState._UNINITIALISED;

        public static void updatePlaybackStateDebugMessage(bool force = false)
        {
            if (!DebugSettings.Playback && !force)
            {
                lastPlaybackMessage = PlaybackState._UNINITIALISED;
                return;
            }

            PlaybackState newPlaybackMessage = GlobalState.playbackState;

            if (newPlaybackMessage == lastPlaybackMessage && !force)
            {
                return;
            }

            if (newPlaybackMessage == PlaybackState.STOPPED)
            {
                Debug.Log("[Playback Stopped]");
            }
            else if (newPlaybackMessage == PlaybackState.STARTED)
            {
                Debug.Log("[Playback Started]");
            }
            else if (newPlaybackMessage == PlaybackState.SCHEDULED)
            {
                Debug.Log("[Playback Scheduled]");
            }

            lastPlaybackMessage = newPlaybackMessage;
        }
    }
}
