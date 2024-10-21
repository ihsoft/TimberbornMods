# TimberbornMods
This solution only contains C# projects to build scripts (DLLs) for the mods. The mod themselves
are made via Unity.

## Prerequisites

1. Read `README.md` in `ModsUnityProject` and folow the steps to prepare the Unity project.
2. Read `README.md` in `Dependencies` and folow the steps to prepare the dependency assemblies.
3. Install .NET8 or a more fresh version. The minimum supported C# compiler version is 12.
4. Ensure that the proper MSBuild version is used by IDE. In JetBrains Rider it's located in:
   "Build, Execution, Deployment | Toolset and Build".
5. Setup a logical drive `u:` pointing to `C:\Users\<user>\Documents\Timberborn` to have the built
   binaries copied automatically into the Timberborn mods folder.

## Development and building

Make the mod from the Unity project. Then, compile the script. This will create a fully complete
mod setup at the target path.
