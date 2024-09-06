# TimberbornMods
This solution only contains C# projects to build scripts (DLLs) for the mods. The mod themseves
are made via Unity. See ModsUnityProject

## Development and building

1. Open Unity project and do "Import Timberborn DLLs".
2. Copy DLLs from "plugins" folder into "Dependencies/Timberborn/plugins".
3. Install dependencies in Steam Workshop: TimberAPI and Harmony.
4. Find depenecies in Steam folder and copy them to "Dependencies/workshop", renaming accordingly.
5. Copy needed Unity DLLs into "Dependencies/Timberborn/Unity" folder. Open C# projects to see
   what's missing.
6. Map a logical disk "U:" to Timberborn local storage: C:/Users/<username>/Documents/Timberborn.

All, but the code changes are made in Unity. Once done, build mod in unity via Timberborn
extension. Then, compile the relevant C# rpoject. The suggested IDE is JetBrains Rider.
