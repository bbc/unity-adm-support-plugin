# Unity ADM Support Plugin
Unity plugin providing ADM rendering support

## Implementation in Unity Projects

Please refer to [UnityAdmProject/Assets/UnityAdm/implementation.md](UnityAdmProject/Assets/UnityAdm/implementation.md)

## Developer Notes

***NOTE:** Built for Unity 2019.4.1f1 - use this version or upgrade the Project (WARNING: upgrading will affect backwards-compatibility!)*

The plugin consists of two parts; C# scripts which integrate with Unity, and a native library which performs the heavy-lifting (file reading, ADM parsing, audio processing, etc.) Unity will compile the C# scripts for you. The native library must be built separately. The following notes provide a baseline workflow to set up a development environment using CMake:

### Windows Preparation

**Download/Install:**

- Boost 1.73 (pre-built, or build locally)

### Mac Preparation

**Get Brew!** (see online) initialise with `brew doctor`
- brew install pkgconfig
- brew install boost

### ALL Operating Systems - Clone and Build

*From the same project directory...*
- `git clone git@github.com:bbc/unity-adm-support-plugin.git`
- `cd .\unity-adm-support-plugin\`
- `git submodule update --init --recursive`
- `mkdir build`

*Open cmake-gui*
- Set Source directory to the repository directory 
- Set Build directory to the `build` subdirectory 
- Select "Configure" - Choose your IDE and architecture
- If not done automatically, set `Boost_INCLUDE_DIR` to your Boost directory
- To enable packaging, set `UNITY_EXECUTABLE` to the path to the Unity executable version you'd like to use for exporting the package
- Select "Configure", then "Generate", then "Open"

*From your IDE...*
- Build target `ALL_BUILD`, then `INSTALL` to copy all build products in to the Unity Project folders

### Prepare Unity project

*Open Unity Hub*
- Projects -> Add
- Select `UnityAdmProject` folder in the repository

## Packages and Packaging

The Unity ADM plugin is packaged as an Asset Package with the naming convention `UnityADM-[version]-[OS]([Arch]).unitypackage`. 

### Creating/updating packages

For both Automatic and Manual packaging method, you must first;

- Build the `ALL_BUILD` cmake target to compile the native library.
- Build the `INSTALL` cmake target to copy the compiled `libunityadm` native library, the `default.tf` data file, and the `LICENSE` and `COPYRIGHT` notices to the correct locations in the Unity project from which the package will be generated.
- Update the readme in the `UnityAdm` folder of the Unity project

The Automatic method is recommended as this will also generate a `VERSION` file.

#### Automatically

Note: For this to work, you must have correctly set the `UNITY_EXECUTABLE` cmake variable

- Tag the commit with a numeric-only tag if it is a version milestone (e.g, "1.0", "1.1", "1.2" etc)
- Build the cmake `PACKAGING` target
- The package will be created in your build directory.

#### Manually through the Unity development environment

- Using the Unity project included in this repository, go to `Assets -> Export Package`. 
- Ensure only items in the `UnityAdm` folder are selected in the dialog that appears. If there is a `VERSION` file present, this MUST NOT be selected as it will be out of date.
- Click "Export" and provide a name according to the aforementioned naming convention.

### Packaging for multiple platforms

The native `libunityadm` library must be compiled for all platforms you wish to support. Ensure each of these built libraries are copied to the `UnityAdm/plugins` folder. Package using the same methods listed above. Simply having those alternative platform libraries in the `UnityAdm/plugins` folder will cause them to be included in the package.
