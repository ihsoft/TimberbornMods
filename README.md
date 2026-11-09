# TimberbornMods

This repository contains the C# projects and shared Unity project used to build Timberborn mods.

## Automation integration for other mods

Automation can expose other mods' signals and actions through JSON configuration, without a helper source or Automation
DLL dependency. Player and modder guides belong in the [Automation Wiki](https://github.com/ihsoft/TimberbornMods/wiki).
The hook integration guide is not published yet. The complete author-facing
[JSON format reference](Automation/ScriptingEngine/ScriptableComponents/Extensions/HookConfiguration.md) is available
for integration authors and the Wiki editor.
Agents authoring or reviewing hook integrations should start with the scoped
[integration instructions](Automation/ScriptingEngine/ScriptableComponents/Extensions/AGENTS.md).
Only maintainers changing the hooks engine need the separate
[implementation notes](Automation/ScriptingEngine/ScriptableComponents/Extensions/HookImplementation.md).

## Setup with an AI agent

Point your agent at this repository and ask:

> Bootstrap this TimberbornMods checkout. Follow `AGENTS.md` and
> `docs/timberborn-new-repository-bootstrap-for-ai-agents.md`, complete everything you can, and tell me which steps
> require my input.

## Manual setup

1. Install the .NET SDK selected by `global.json`. Ensure that your IDE uses a compatible MSBuild toolset.
2. Create the local `_GAME!`, `_WORKSHOP!`, `_MODS!`, and `_LOGS!` links, plus `Dependencies/GameRoot` and
   `Dependencies/Workshop`. Their targets are described in the
   [bootstrap guide](docs/timberborn-new-repository-bootstrap-for-ai-agents.md) and
   [dependency setup](Dependencies/README.md).
3. Install the Unity Editor version from `ModsUnityProject/ProjectSettings/ProjectVersion.txt` with Windows and Mac
   Build Support. Sign in to Unity Hub and make sure the Editor license is active.
4. Follow the official [Unity setup](https://github.com/mechanistry/timberborn-modding/wiki/Unity-setup) and
   [asset importer](https://github.com/mechanistry/timberborn-modding/wiki/Asset-importer) workflow. Add
   `ModsUnityProject` to Hub with `-disable-assembly-updater`, choose `Ignore` instead of Safe Mode on first open, and
   import the Timberborn DLLs and assets from the game installation.

## Development and building

Build each mod through its owning projects. For Unity-owned data, export `ModsUnityProject` content into `_MODS!`, then
build the corresponding C# project so its DLLs are copied into the exported package. Repository helpers for these
workflows live under `tools/`.
