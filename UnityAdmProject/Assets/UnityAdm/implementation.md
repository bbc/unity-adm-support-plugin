# QUICK-START GUIDE

- Set Project Settings to "Allow 'unsafe' code" ("Player" options under "Edit > Project Settings"). This is simply to allow direct access to the audio buffers used by Unity.
- Import the Unity package ("Assets > Import Package > Custom Package...")
- Attach to the Main Camera (Drag and drop the UnityAdm Script from the Assets/UnityAdm folder in to the Inspector whilst the Main Camera is selected)
- Drag an ADM clip on to the "ADM Audio Clip" parameter in the Inpector for the UnityAdm script
- Check the "Start Playback On Project Run" checkbox in the Inpector for the UnityAdm script
- Run!



# FULL IMPLEMENTATION OF PACKAGE IN UNITY PROJECTS

## Import the Package

- Steps may vary based on Unity version. This is based on 2019.4.1f1.
- In the Unity Editor with a project open, go to "Edit> Project Settings". Select "Player" in the left-hand pane. Check "Allow 'unsafe' code". Unsafe code is essentially code which attempts to directly access data stored in memory by address. In this plugin, this technique is used for efficiency. It allows the native library to access the metadata and audio buffers used within Unity without the overhead of copying back-and-forth. Code which accesses data this way has to be very carefully designed to avoid addresses becoming stale if data is destroyed or moved in memory by a concurrent process, hence the term 'unsafe'.
- Unity provides an ‘Import Package’ feature which handles the installation process, copying the required files into the correct locations in the project. From the menu bar, "Assets > Import Package > Custom Package..." and select the .unitypackage file.
- In the "Import Unity Package" dialog which appears, ensure all items are selected and click "Import"
- In your projects "Assets" folder, you should now have a new "UnityAdm" folder

## Add the Plugin to your project

- The Unity ADM Plugin can be added to any GameObject - it doesn't really matter what. The plugin will pull listener position and orientation data from the Main Camera object regardless of which GameObject the plugin is running on. Main Camera normally makes a good host object for the script.
- With a GameObject selected, grab the UnityAdm script from inside the Assets/UnityAdm folder in the Project pane and drop it in the Inspector pane (over the "Add Component" button.)
- Ensure Project setting are appropriate to run the plugin ("Edit > Project Settings > Audio"). The DSP buffer size can be adjusted towards better latency or better performance. The ideal setting will depend upon the overall workload presented by the application and the target platform. The ‘Max Real Voices’ setting will essentially limit the number of AudioSources that can be playing audio within the scene. This should be set high enough to accommodate the ADM media used in the project, and any additional sources. If the plugin is configured to use BEAR, this will use only one AudioSource regardless of what ADM media is being rendered.

## Use with XR hardware

- The plugin will work with most XR hardware which operates by controlling the position and orientation of the Main Camera, since this is where the plugin pulls listener data from. This includes any hardware which is supported by SteamVR.
- If using SteamVR, the Unity ADM plugin can be configured to integrate with it to recentre the listener position. This is achieved by ensuring the "Interface with SteamVR" checkbox is selected in the "Unity ADM live controls" pane ("Window > Unity ADM live controls"). This will trigger a recompile and if SteamVR is not found, an appropriate error message will be displayed in the Console pane.

## Configuring the Plugin

- The plugin can be configured from the Inspector pane once the main script (UnityAdm) has been placed on a Game Object.
- Parameters have tooltip hints to explain their purpose - hover over a parameter to read a description.
- To assign the ADM media to play back, an audio clip from the project can be dropped on to the "ADM Audio Clip" parameter, or a file path can be provided using the "ADM Audio Path" parameter (please see notes below regarding use of paths in built projects).
- Most parameters can be controlled programatically (when parameters are changed at runtime, `applySettings()` should be called, followed by `initialise()`). The most important parameters are;
	- `string ADMAudioPath`; This is the path to the ADM media to be rendered. You should ensure this is portable within your project and within built projects (see Building notes below)
	- `uint BEARMaxObjectChannels`, `uint BEARMaxDirectSpeakersChannels` and `uint BEARMaxHoaChannels`; When BEAR is constructed, it will reserve a specific number of input channels and audio processing pipelines for different types of input, which can be configured with these parameters. If these numbers are set lower than required by the input media, some audio sources will be omitted from the render. 

## Controlling the Plugin during runtime

- There are controls available for testing the plugin during runtime. They are accessed via the menu bar; "Window > Unity ADM live controls"

## Programmatically Controlling the Plugin during runtime

- There are several API methods available, and the code below shows how these methods and properties might be accessed:

```
var script = FindObjectOfType<UnityAdm>();
script.stopPlayback();
script.BEARMaxObjectChannels = 15;
script.applySettings();
script.initialise();
```

- The API methods are;
	- `applySettings()`; Used to read current properties, validate and apply them. Most properties will not take effect until initialiase is called.
	- `initialise()`; This will essentially restart the plugin, which is useful to apply new settings at runtime, load a new file, or to seek to another point in the file by simply beginning again with a new start offset.
	- `startPlayback()` and `stopPlayback()`; self-explanatory: they will start and stop playback as soon as possible. Note that starting playback in this manner will still have some delay – it will be fast but cannot possibly be instantaneous.
	- `schedulePlayback(double forDspTime)`; Precisely synchronise playback against the DSP clock.
	- `recentreListener()`;reset the listener position and orientation readings from XR headset. This is only functional if the plugin has been compiled with SteamVR support.

## Building Unity Projects with the plugin

When building a Unity project as an executable, some preparation is required;

- When a file containing ADM media is dragged on the to plugin script in the Inspector pane, the absolute path of that file is determined and placed in the "ADM Audio Path" field. Although this is fine as a quick development feature, an absolute path to a file in the project directory (or subdirectory) is not portable - you should use a relative path. This can be set programmatically (`UnityAdm.ADMAudioPath` string).

# LIMITATIONS

- Only one instance of the Unity ADM Plugin can be present in a project - it currently doesn't support multiple instances.
- It is currently not possible to seek during playback - a start offset can be specified but the plugin must be reinitialised to enact it.
- There currently isn't a "looping" feature. 
