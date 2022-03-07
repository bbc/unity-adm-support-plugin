# unity-adm-support-plugin
 Unity plugin providing ADM rendering support

## Installation

Obviously there are multiple ways to do the following steps, but they provide a baseline workflow;

### Windows Preparation

**Download/Install:**

- Boost 1.73 pre-built

### Mac Preparation

**Get Brew!** (see online) initialise with `brew doctor`
- brew install pkgconfig
- brew install boost

### ALL Operating Systems

**Generate SSH Keys**

Need logins and SSH keys for;
- github.com

To generate and upload keys;
- *In `~/.ssh/`*
- `ssh-keygen -t rsa -b 4096 -C "email@address"`
- Upload .pub content to relevent Git server
- Update `~/.ssh/config` with;

```
        Host eg-github.com
          IdentityFile ~/.ssh/eg-github
```

### Clone and Build unity-adm-support-plugin

*From the same project directory...*
- `git clone git@github.com:bbc/unity-adm-support-plugin.git`
- `cd .\unity-adm-support-plugin\`
- `git submodule update --init --recursive`
- `mkdir build`

*Open cmake-gui*
- Set Source directory to the `unity-adm-support-plugin` repository directory 
- Set Build directory to the `unity-adm-support-plugin/build` directory 
- "Configure"
- If not done automatically, set `Boost_INCLUDE_DIR` to your Boost directory
- To enable packaging, set `UNITY_EXECUTABLE` to the path to the Unity executable version you'd like to use for exporting the package
- "Configure", "Generate", "Open"

*From your IDE...*
- Build target `ALL_BUILD`, then `INSTALL` to copy all build products in to the Unity Project folders

### Prepare Unity project

*Open Unity Hub*
- Projects -> Add
- Select "Unity" folder in unity-adm-support-plugin repository

***NOTE:** Built for Unity 2019.4.1f1 - use this version or upgrade the Project (WARNING: will affect everyone if you commit it back!!)*
---------
## Packages and Packaging

The Unity ADM plugin is packaged as an Asset Package with the naming convention `UnityADM-[version]-[OS]([Arch]).unitypackage`. 

### Use in existing projects
If you'd like to use unity, the plugin will expect to find the SteamVR package within the same project, so ensure you download that from the asset store and import it.

Add the Unity ADM package to your existing project via the menu; `Assets -> Import Package -> Custom Package`. Then simply apply the `UnityAdm.cs` script to an object in the scene (usually main camera is fine). Configure it via the Inspector.

### Creating/updating packages

For both Automatic and Manual packaging method, you must first;

- Configure and Generate using cmake - this will create build files for the native library.
- Build the "ALL_BUILD" cmake target to compile the native library.
- Build the "INSTALL" cmake target to copy the compiled `libunityadm` native library, the `default.tf` data file, and the LICENSE and COPYRIGHT notices to the correct locations in the Unity project from which the package will be generated.
- Update the readme in the `UnityAdm` folder of the Unity project

The Automatic method is recommended as this will also generate a VERSION file.

#### Automatically

Note: For this to work, you must have correctly set the `UNITY_EXECUTABLE` Cmake variable

- Tag the commit if it is a version milestone
- Build the cmake "PACKAGING" target

#### Manually through the Unity development environment

- Using the Unity project included in this repository, go to `Assets -> Export Package`. 
- Ensure only items in the `UnityAdm` folder are selected in the dialog that appears. If there is a VERSION file present, this MUST NOT be selected as it will be out of date.
- Click Export and provide a name according to the aforementioned naming convention.
