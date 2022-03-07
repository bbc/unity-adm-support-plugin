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

The Unity ADM plugin is packaged as an Asset Package with the naming convention `UnityADM-([os]_[arch]).unitypackage`. 

### Use in existing projects
The plugin will expect to find the SteamVR package within the same project, so ensure you download that from the asset store and import it.

Add the Unity ADM package to your existing project via the menu; `Assets -> Import Package -> Custom Package`. Then simply apply the `UnityAdm.cs` script to an object in the scene (usually main camera is fine). Configure it via the Inspector.

### Creating/updating packages
Firstly, ensure you have built release versions of the native plugin (`libunityadm`) for the platform(s) you wish to support. Run the CMake `INSTALL` target to ensure the built library is copied to the correct location as well as the `default.tf` data file.

Secondly, update the readme in the `UnityAdm` folder!

Using the Unity project included in this repository, go to `Assets -> Export Package`. Ensure only items in the `UnityAdm` folder are selected. Select export and provide a name according to the aforementioned naming convention.
