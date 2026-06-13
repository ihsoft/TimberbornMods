# Timberborn New Repository Bootstrap for AI Agents

## Purpose

Use this document when a new Timberborn mods repository has only copied:

- `AGENTS.md`
- `docs/`

from another Timberborn mods repository, and the user asks to set up the repository or create the first mod.

The goal is to connect the copied rules to the new local environment before implementing gameplay features.

Do not assume the original repository layout exists.

Do not assume the example mod idea is literal.

If the user says something like "create a transparent ground mod like X-Ray", treat X-Ray as an example of the kind of
feature they want, not as a command to copy an existing project. The same bootstrap and research workflow applies to
any requested first mod.

---

## First Steps

Before creating the first mod:

1. Read `AGENTS.md`.
2. Read the relevant files under `docs/`.
3. Inspect the repository contents.
4. Identify what is missing:
   - `.gitignore`,
   - `tools/`,
   - local game links,
   - generated reference folders,
   - solution/project files,
   - package-data folders,
   - existing examples.
5. Ask the user for local paths that cannot be discovered safely.

Do not invent local paths.

Do not create a mod project until local references and the expected project layout are clear.

---

## Local Links

Local links make the repository portable while keeping machine-specific paths out of Git.

Ask the user where these links should point:

| Link | Target |
| --- | --- |
| `_GAME!` | Timberborn game installation root |
| `_WORKSHOP!` | Steam Workshop content folder for Timberborn app id `1062090` |
| `_MODS!` | User Timberborn mods folder |
| `_LOGS!` | Timberborn LocalLow folder with logs and settings |

Common Windows examples:

```text
_GAME!      -> R:\Program Files (x86)\Steam\steamapps\common\Timberborn
_WORKSHOP!  -> R:\Program Files (x86)\Steam\steamapps\workshop\content\1062090
_MODS!      -> %USERPROFILE%\Documents\Timberborn\Mods
_LOGS!      -> %USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn
```

The actual paths are user-specific. Ask before creating links.

On Windows, prefer directory junctions from the repository root:

```powershell
New-Item -ItemType Junction -Path "_GAME!" -Target "<Timberborn game root>"
New-Item -ItemType Junction -Path "_WORKSHOP!" -Target "<Steam workshop content 1062090>"
New-Item -ItemType Junction -Path "_MODS!" -Target "<Documents\Timberborn\Mods>"
New-Item -ItemType Junction -Path "_LOGS!" -Target "<AppData\LocalLow\Mechanistry\Timberborn>"
```

Alternative `cmd.exe` syntax:

```cmd
mklink /J "_GAME!" "<Timberborn game root>"
mklink /J "_WORKSHOP!" "<Steam workshop content 1062090>"
mklink /J "_MODS!" "<Documents\Timberborn\Mods>"
mklink /J "_LOGS!" "<AppData\LocalLow\Mechanistry\Timberborn>"
```

Before creating a link:

1. Check whether the path already exists.
2. If it exists and points to the expected target, keep it.
3. If it exists and points elsewhere, ask before changing it.
4. If it is a real directory, do not delete or replace it without explicit approval.

---

## Ignored Local and Generated Paths

Ensure `.gitignore` ignores:

```text
.tools/
_GAME!
_WORKSHOP!
_MODS!
_LOGS!
_DecompiledGame/
_ExtractedGameAssets/
*.DotSettings.user
```

`tools/` should be tracked.

`.tools/`, local links, and generated reference folders should not be tracked.

---

## Helper Scripts

If missing, add repository helper scripts under `tools/`.

Recommended scripts:

- `tools/decompile-game.ps1`
- `tools/extract-game-modding-assets.ps1`

`tools/decompile-game.ps1` should:

- read game assemblies from a local game reference, commonly `Dependencies/GameRoot/Timberborn_Data/Managed` or
  `_GAME!/Timberborn_Data/Managed`,
- install or use `ilspycmd` under `.tools/ilspy`,
- output generated sources to `_DecompiledGame/`.

`tools/extract-game-modding-assets.ps1` should:

- read archives from `_GAME!/Timberborn_Data/StreamingAssets/Modding/`,
- extract `Blueprints.zip`, `Localizations.zip`, `Shaders.zip`, and `UI.zip`,
- output generated references to `_ExtractedGameAssets/`.

Treat both output folders as read-only generated references.

---

## Generated References

When local links are ready, generate read-only references if needed:

- `_DecompiledGame/` for game C# architecture,
- `_ExtractedGameAssets/Blueprints/` for game blueprint/component-spec patterns,
- `_ExtractedGameAssets/Localizations/` for existing localization keys and game terminology,
- `_ExtractedGameAssets/UI/` for UXML, USS, sprites, stable element names, and UI hierarchy,
- `_ExtractedGameAssets/Shaders/` for shader-specific work.

Use these references for research.

Do not edit generated files.

---

## First Mod Project Rules

When creating the first mod project, do not copy a project layout blindly.

First:

1. Understand the requested feature.
2. Find the closest existing game feature.
3. Study game data and decompiled classes.
4. Trace data/spec to component, service, UI, persistence, and registration.
5. Decide whether the mod needs code, blueprints, Unity assets, UI assets, Harmony patches, or a combination.

Then create the smallest project layout that fits the feature and the repository rules.

For C# mod projects:

- configure Timberborn assembly references,
- include `BepInEx.AssemblyPublicizer.MSBuild` when private/internal game access is expected,
- set `Publicize="true"` on relevant Timberborn references when direct access is needed,
- prefer publicized direct member access over reflection or `AccessTools`,
- use reflection only when publicized direct access is unavailable or unsuitable,
- use Harmony only when no reasonable extension point exists.

For package data:

- add `manifest.json`,
- add localization files for user-facing text,
- add blueprints/assets/UI files only when required by the feature,
- decide whether a Unity project is needed before creating one.

---

## User Questions to Ask

Ask concise questions for missing local setup information:

- Where is the Timberborn game installation?
- Where is the Timberborn Steam Workshop content folder?
- Where is the user Timberborn mods folder?
- Where is the Timberborn LocalLow/logs folder?
- Should local links be created as Windows junctions?
- Should game assemblies be referenced through `_GAME!`, copied into `Dependencies/GameRoot`, or linked another way?
- What should the first mod do, in player-visible terms?

After the user answers, set up the repository and continue with the normal Timberborn modding workflow.
