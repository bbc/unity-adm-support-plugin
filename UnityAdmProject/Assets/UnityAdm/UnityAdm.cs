using UnityEngine;
using UnityEditor;
using ADM;

public class UnityAdm : MonoBehaviour
{
    private AudioClip lastSetAdmAudioClip;
    private string lastSetAdmAudioPath;

    [Header("Input Source")]
    public AudioClip ADMAudioClip;
    public string ADMAudioPath;

    [Header("Game Objects")]
    public bool useVisualisations = true;
    public GameObject objectVisualisation;
    public GameObject speakerVisualisation;
    public GameObject hoaVisualisation;
    [Tooltip("The default value to multiply coordinates with in order to convert to real-world distances.\n" +
        "This value will be used if 'alwaysOverrideAbsoluteDistance' is checked or if an AudioPackFormat does not specify its own AbsoluteDistance value.\n" +
        "This will also affect any distance-related effects when using Unity native rendering.\n" +
        "(* Can be changed at runtime, but only affects newly received metadata blocks (i.e, not for file-based ADM))")]
    public float defaultReferenceDistance = 1.0f;
    [Tooltip("Always overrides AbsoluteDistance with the value of 'Default Reference Distance', whether originally present or not.\n" +
       "(* Can be changed at runtime, but only affects newly received metadata blocks (i.e, not for file-based ADM))")]
    public bool alwaysOverrideAbsoluteDistance = false;

    [Header("Audio Renderer")]
    [Tooltip("Audio renderer to use\n" +
        "\n - Unity native renderer only supports Object and Direct Speaker types. Also due to the GameObject update loop, position and gain changes could be up to a frame behind the audio." +
        "\n - BEAR renderer supports Objects, DirectSpeakers and HOA.")]
    public AudioRendererType audioRendererType = AudioRendererType.BEAR;
    [Header("Audio Renderer - BEAR Specific Settings")]
    [Tooltip("(* Can be changed at runtime)")]
    public float BEAROutputGain = 0.775f;
    [Tooltip("Note that the buffer/block size of the output audio device will also affect latency.\n" +
        "Also note that powers of two are likely to be far more performant.")]
    public uint BEARInternalBlockSize = 1024;
    public uint BEARMaxObjectChannels = 32;
    public uint BEARMaxDirectSpeakerChannels = 24;
    public uint BEARMaxHoaChannels = 16;
    [Tooltip("Use this string to specify a specfic FFT library for BEAR to use. A blank string will fall back to the default implementation.")]
    public string BEARUseFFTImplementation = "ffts";
    [Tooltip("Provide a path to the *.tf file used by BEAR for binaural rendering. If unspecified, it will default to /Assets/UnityADM/Data/default.tf")]
    public string BEARDataFilePath = "";
    [Tooltip("If the media sample rate does not match the project sample rate, sample rate conversion will be necessary.\n" +
        "Choose the SRC method here.\n" +
        "(* Can be changed at runtime)")]
    public SrcType BEARUseSRCType = SrcType.SincMediumQuality;
	[Tooltip("(* Can be changed at runtime)")]
    public bool BEARRenderOnlySelectedAudioProgramme = false;
    [Tooltip("This is the ID of the AudioProgramme to render (e.g, AP_1001) if the above checkbox is checked.\n" +
        "(* Can be changed at runtime)")]
    public string BEARSelectedAudioProgrammeID = "";

    [Header("Audio Playback")]
    public bool startPlaybackOnProjectRun = false;
    [Tooltip("The starting playhead position (in seconds) within the ADM media when playback begins.\nA negative value will apply a delay before the media begins playing.")]
    public double startingPlayheadPosition = 0.0f;
    [Tooltip("The time given (in seconds) to allow setup and scheduling of audio playback for newly discovered audioObjects.\n" +
        "Therefore this concerns audioObjects discovered by the metadata pull thread and not those discovered initially.\n" +
        "\n - If this is too short, the scheduled time may pass before setup has completed, and the audio may become out of sync." +
        "\n - If this is too long, the start of the audio for this object may be missed and replaced with silence.")]
    public double schedulingWindowTime = 0.5f;

    [Header("Metadata")]
    [Tooltip("A thread can be started to check for new metadata dynamically during playback." +
        "\nThis is not required for file-based ADM since all metadata is available immediately on file load." +
        "\nFor serialised ADM (future work) this would be essential to follow realtime updates to the metadata.")]
    public bool runMetadataPullThread = false;
    [Tooltip("To throttle the metadata pull thread, a sleep period can be specified (in ms) between checks." +
        "\nNote that only one block is pulled per check and so this period should be based upon the assumed overall block rate of the stream," +
        " otherwise the metadata may fall behind (time to enact already passed by the time it is retrieved.)" +
        "\nAlso note that 0 sleep period is often fine given that this is running in a thread.")]
    public uint metadataPullThreadCheckPeriod = 5;

    public double directSpeakersUnityXOffset = 0.0;
    [Tooltip("Y axis in Unity (Z axis in ADM!)")]
    public double directSpeakersUnityYOffset = 0.0;
    [Tooltip("Z axis in Unity (Y axis in ADM!)")]
    public double directSpeakersUnityZOffset = 0.0;
    public double directSpeakersAzimuthOffset = 0.0;
    public double directSpeakersElevationOffset = 0.0;
    public double directSpeakersDistanceMultiplier = 1.0;

    public double objectsUnityXOffset = 0.0;
    [Tooltip("Y axis in Unity (Z axis in ADM!)")]
    public double objectsUnityYOffset = 0.0;
    [Tooltip("Z axis in Unity (Y axis in ADM!)")]
    public double objectsUnityZOffset = 0.0;
    public double objectsAzimuthOffset = 0.0;
    public double objectsElevationOffset = 0.0;
    public double objectsDistanceMultiplier = 1.0;

#if STEAMVR
    [Header("VR System")]
    [Tooltip("Keyboard key to reset the listener to be at the coordinate system origin.")]
    public KeyCode recentreListenerKey = KeyCode.R;
#endif

    private void Awake()
    {
    }

    private void Start()
    {
        // Find HMD
        OpenVrWrapper.updateHmdIndex();

        applySettings();
        initialise();

        if (startPlaybackOnProjectRun)
        {
            schedulePlayback(AudioSettings.dspTime + schedulingWindowTime + 2.0);
            // Playback scheduled time needs to be at least current time + the required pre-warn to schedule sources (scheduling window)
            // However, there is going to be a delay from this call to the first update, which then also has a bunch of other code to run before scheduling initial sources.
            // Also Unity seems to get weighed down a fair bit when you first hit "Play", so we need to let that settle out a bit.
            // Therefore, the additional couple of seconds here is to prevent warnings of not enough time for scheduling window
            /// (because we'd have already eaten in to it by the time we're scheduling the initial sources.)
            // A "real" call to schedulePlayback would be expected to set the scheduled time far enough ahead to allow for scheduling and other processing
            /// (but probably doesn't need to consider that initial Unity "play" lag.)
        }
        DebugPlaybackTracker.updatePlaybackStateDebugMessage();

        if(GlobalState.runThread) GlobalState.metadataHandler.startBlockPullThread();
    }

    private void Update()
    {
#if STEAMVR
        if (Input.GetKeyDown(recentreListenerKey) == true) recentreListener();
#endif

        if (GlobalState.audioRenderer != null) GlobalState.audioRenderer.onUpdateBegin();

        DebugPlaybackTracker.updatePlaybackStateDebugMessage();
        if (GlobalState.playing)
        {
            GlobalState.metadataHandler.createPreparedItems();
            GlobalState.metadataHandler.dispatchLatestMetadata();
        }

        if (GlobalState.audioRenderer != null) GlobalState.audioRenderer.onUpdateEnd();
    }

    private void OnValidate()
    {
        applySettings();
    }

    // ------------------- API METHODS -----------------------

    public void startPlayback()
    {
        // Use this method to start playback without synchronising to the DSP clock

        schedulePlayback(AudioSettings.dspTime + GlobalState.schedulingWindow);
    }

    public void schedulePlayback(double forDspTime)
    {
        // Use this method to schedule playback against the DSP clock

        if (GlobalState.playbackState == PlaybackState.SCHEDULED || GlobalState.playbackState == PlaybackState.STARTED)
        {
            stopPlayback();
        }

        GlobalState.setPlaybackScheduled(forDspTime);
        if(GlobalState.audioRenderer != null) GlobalState.audioRenderer.scheduleAudioPlayback(forDspTime);
        DebugPlaybackTracker.updatePlaybackStateDebugMessage();
    }

    public void stopPlayback()
    {
        // Use this method to stop playback

        if (GlobalState.playbackState == PlaybackState.STOPPED) return;

        GlobalState.setPlaybackStopped();
        if (GlobalState.audioRenderer != null) GlobalState.audioRenderer.stopAudioPlayback();
        DebugPlaybackTracker.updatePlaybackStateDebugMessage();
    }

    public void applySettings()
    {
        // Use this method to validate and apply settings whenever any public variables within this class are changed.
        // Most of these settings will not take effect until playback is restarted.

        // Determine input source depending on whether Clip or path was changed
#if UNITY_EDITOR
        if (ADMAudioClip != lastSetAdmAudioClip)
        {
            lastSetAdmAudioClip = ADMAudioClip;
            if (ADMAudioClip)
            {
                string relPath = AssetDatabase.GetAssetPath(ADMAudioClip);
                string fullPath = PathHelpers.GetApplicationPath() + relPath;

                if (DebugSettings.AdmSourceSetting) Debug.Log("admAudioClip set. Setting path... " + fullPath);
                lastSetAdmAudioPath = fullPath;
            }
            else
            {
                lastSetAdmAudioPath = "";
            }
            ADMAudioPath = lastSetAdmAudioPath;

        }
        else
#endif
        {
            if (!StringHelpers.nullsafeEqualityCheck(ADMAudioPath, lastSetAdmAudioPath))
            {
                lastSetAdmAudioPath = ADMAudioPath;
                if (DebugSettings.AdmSourceSetting) Debug.Log("admAudioPath set. Resetting AudioClip...");
                ADMAudioClip = null;
                lastSetAdmAudioClip = null;
            }
        }

        // Update BEAR output gain if BEAR renderer selected
        if (GlobalState.audioRendererType == AudioRendererType.BEAR)
        {
            LibraryInterface.setBearOutputGain(BEAROutputGain);
        }
        GlobalState.BearOutputGain = BEAROutputGain;
        if (BEARMaxObjectChannels > 128) BEARMaxObjectChannels = 128;
        if (BEARMaxDirectSpeakerChannels > 128) BEARMaxDirectSpeakerChannels = 128;
        if (BEARMaxHoaChannels > 128) BEARMaxHoaChannels = 128;
        if (BEARInternalBlockSize < 64) BEARInternalBlockSize = 64;
        if (BEARInternalBlockSize > 8192) BEARInternalBlockSize = 8192;
        if (defaultReferenceDistance < 0.0f) defaultReferenceDistance = 1.0f;
        GlobalState.defaultReferenceDistance = defaultReferenceDistance;
        GlobalState.alwaysOverrideAbsoluteDistance = alwaysOverrideAbsoluteDistance;

        // AudioProgramme selection for BEAR

        bool apNeedUpdate = BEARRenderOnlySelectedAudioProgramme != GlobalState.renderOnlySelectedAudioProgramme;
        int BEARSelectedAudioProgrammeIDAsInt = 0;
        if (BEARRenderOnlySelectedAudioProgramme) {
            BEARSelectedAudioProgrammeIDAsInt = IdHelpers.intFromAudioProgrammeId(BEARSelectedAudioProgrammeID);
            if (BEARSelectedAudioProgrammeIDAsInt != GlobalState.selectedAudioProgrammeId)
            {
                apNeedUpdate = true;
            }
        }
        if(apNeedUpdate)
        {
            GlobalState.selectedAudioProgrammeId = BEARSelectedAudioProgrammeIDAsInt;
            GlobalState.renderOnlySelectedAudioProgramme = BEARRenderOnlySelectedAudioProgramme;
            ADM.BearAudioRenderer renderer = ADM.GlobalState.audioRenderer as ADM.BearAudioRenderer;
            if (renderer != null)
            {
                if (GlobalState.renderOnlySelectedAudioProgramme)
                {
                    renderer.setAudioProgrammeId(GlobalState.selectedAudioProgrammeId);
                }
                else
                {
                    renderer.resetAudioProgrammeId();
                }
            }
        }

        // Offsets

        while (directSpeakersAzimuthOffset > 180.0) directSpeakersAzimuthOffset -= 360.0;
        while (directSpeakersAzimuthOffset <= -180.0) directSpeakersAzimuthOffset += 360.0;
        while (directSpeakersElevationOffset > 90.0) directSpeakersElevationOffset = 90.0;
        while (directSpeakersElevationOffset < -90.0) directSpeakersElevationOffset = -90.0;
        if (directSpeakersDistanceMultiplier < 0.0) directSpeakersDistanceMultiplier = 1.0;

        while (objectsAzimuthOffset > 180.0) objectsAzimuthOffset -= 360.0;
        while (objectsAzimuthOffset <= -180.0) objectsAzimuthOffset += 360.0;
        while (objectsElevationOffset > 90.0) objectsElevationOffset = 90.0;
        while (objectsElevationOffset < -90.0) objectsElevationOffset = -90.0;
        if (objectsDistanceMultiplier < 0.0) objectsDistanceMultiplier = 0.0;

        GlobalState.directSpeakersXOffset = directSpeakersUnityXOffset;
        GlobalState.directSpeakersYOffset = directSpeakersUnityZOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.directSpeakersZOffset = directSpeakersUnityYOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.directSpeakersAzimuthOffset = directSpeakersAzimuthOffset;
        GlobalState.directSpeakersElevationOffset = directSpeakersElevationOffset;
        GlobalState.directSpeakersDistanceMultiplier = directSpeakersDistanceMultiplier;
        GlobalState.objectsXOffset = objectsUnityXOffset;
        GlobalState.objectsYOffset = objectsUnityZOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.objectsZOffset = objectsUnityYOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.objectsAzimuthOffset = objectsAzimuthOffset;
        GlobalState.objectsElevationOffset = objectsElevationOffset;
        GlobalState.objectsDistanceMultiplier = objectsDistanceMultiplier;

    }

	public void recentreListener()
	{
		Debug.Log("Recentring listener");
        OpenVrWrapper.recentreListener();
	}

    public void shutdown()
    {
        // Use this method to destroy existing modules.
        // initialise MUST be called following this before using any further functionality

        if (GlobalState.playbackState == PlaybackState.SCHEDULED || GlobalState.playbackState == PlaybackState.STARTED)
        {
            Debug.LogError("Can not shutdown() when playing!");
            return;
        }

        if (GlobalState.gameObjectHandler != null)
        {
            GlobalState.gameObjectHandler.shutdown();
            GlobalState.gameObjectHandler = null;
        }

        if (GlobalState.audioRenderer != null)
        {
            GlobalState.audioRenderer.shutdown();
            GlobalState.audioRenderer = null;
        }

        if (GlobalState.metadataHandler != null)
        {
            GlobalState.metadataHandler.shutdown();
            GlobalState.metadataHandler = null;
        }

    }

    public void initialise()
    {
        // Use this method to destroy and reconstruct modules according to current settings.
        // Will also be done automatically on "Start".
        // This is particularly important for BEAR which needs restarting in order to jump to another place in the audio.

        if(GlobalState.playbackState == PlaybackState.SCHEDULED || GlobalState.playbackState == PlaybackState.STARTED)
        {
            Debug.LogError("Can not initialise() when playing!");
            return;
        }

        shutdown();

        GlobalState.startingAdmPlayheadPosition = startingPlayheadPosition;
        GlobalState.schedulingWindow = schedulingWindowTime;
        GlobalState.runThread = runMetadataPullThread;
        GlobalState.threadCyclePeriodMs = (int)metadataPullThreadCheckPeriod;
        GlobalState.useVisualisations = useVisualisations;
        GlobalState.objectVisualisation = objectVisualisation;
        GlobalState.dsVisualisation = speakerVisualisation;
        GlobalState.hoaVisualisation = hoaVisualisation;
        GlobalState.audioRendererType = audioRendererType;
        GlobalState.BearMaxObjectChannels = (int)BEARMaxObjectChannels;
        GlobalState.BearMaxDirectSpeakerChannels = (int)BEARMaxDirectSpeakerChannels;
        GlobalState.BearMaxHoaChannels = (int)BEARMaxHoaChannels;
        GlobalState.BearDataFilePath = BEARDataFilePath;
        GlobalState.BearFftImplementation = BEARUseFFTImplementation;
        GlobalState.BearOutputGain = BEAROutputGain;
        GlobalState.BearInternalBlockSize = (int)BEARInternalBlockSize;
        GlobalState.defaultReferenceDistance = defaultReferenceDistance;
        GlobalState.alwaysOverrideAbsoluteDistance = alwaysOverrideAbsoluteDistance;
        GlobalState.BearSrcType = BEARUseSRCType;

        GlobalState.directSpeakersXOffset = directSpeakersUnityXOffset;
        GlobalState.directSpeakersYOffset = directSpeakersUnityZOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.directSpeakersZOffset = directSpeakersUnityYOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.directSpeakersAzimuthOffset = directSpeakersAzimuthOffset;
        GlobalState.directSpeakersElevationOffset = directSpeakersElevationOffset;
        GlobalState.directSpeakersDistanceMultiplier = directSpeakersDistanceMultiplier;
        GlobalState.objectsXOffset = objectsUnityXOffset;
        GlobalState.objectsYOffset = objectsUnityZOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.objectsZOffset = objectsUnityYOffset; // NOTE Y/Z SWITCH (Unity vs ADM)
        GlobalState.objectsAzimuthOffset = objectsAzimuthOffset;
        GlobalState.objectsElevationOffset = objectsElevationOffset;
        GlobalState.objectsDistanceMultiplier = objectsDistanceMultiplier;

        if (GlobalState.createIndividualGameObjects())
        {
            GlobalState.gameObjectHandler = new GameObjectHandler();
        }

        if (ADMAudioPath == null || ADMAudioPath.Trim() == "")
        {
            if (GlobalState.metadataHandler != null) GlobalState.metadataHandler.shutdown();
            GlobalState.metadataHandler = null;
            Debug.LogWarning("No audio file to load. Set the 'ADM Audio Path', or drag an Audio Clip in to the 'ADM Audio Clip' field in this scripts Inspector.");
        }
        else
        {
            if (GlobalState.metadataHandler == null || !StringHelpers.nullsafeEqualityCheck(GlobalState.metadataHandler.currentFile, ADMAudioPath))
            {
                if (GlobalState.metadataHandler != null) GlobalState.metadataHandler.shutdown();
                GlobalState.metadataHandler = new MetadataHandler();
                if (!GlobalState.metadataHandler.readFile(ADMAudioPath))
                {
                    Debug.LogError("Error using file '" + ADMAudioPath + "'");
                }
            }
        }
        GlobalState.metadataHandler.reset();

        if (GlobalState.audioRendererType == AudioRendererType.UNITY)
        {
            GlobalState.audioRenderer = new UnityAudioRenderer();
        }
        else if (GlobalState.audioRendererType == AudioRendererType.BEAR)
        {
            ADM.BearAudioRenderer renderer = new BearAudioRenderer();
            if (GlobalState.renderOnlySelectedAudioProgramme)
            {
                renderer.setAudioProgrammeId(GlobalState.selectedAudioProgrammeId);
            }
            GlobalState.audioRenderer = renderer;
            LibraryInterface.setBearOutputGain(BEAROutputGain);
        }

        GlobalState.metadataHandler.getBlocksInitialPull();
    }

    void OnDestroy()
    {
        stopPlayback();
        shutdown();
    }

}


