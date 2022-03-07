ADM plugin for Unity - v1.1

Requirements:
-------------

Unity 2019.4.1f1 or later
For SteamVR integration, the SteamVR plugin for Unity (min v1.2.3) must also be imported.



Change Log:
-----------
- VISR now statically linked. No need for shared libs install.
- Cmake PACKAGING target configured to build .unitypackage with versioning info
- LibBW64 dependency updated (sec fixes)
- Y/Z Coord offsetting params switched (ADM/Unity mismatch - use Unity coordinate system to avoid confusion)
- Coord offsetting params renamed to avoid accidental setting of wrong parameter after Y/Z switch
- Ability to specify BEAR's data file location to avoid having to bundle in projects