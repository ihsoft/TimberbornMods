# Timberborn New Repository Bootstrap for AI Agents

## Purpose

Use this document when a new Timberborn mods repository has only copied:

- `AGENTS.md`
- `docs/`

from another Timberborn mods repository, and the user asks to set up the repository or create the first mod.

Also use it when an existing TimberbornMods checkout is on a clean disk and local build/release prerequisites need to
be recreated.

The goal is to connect the copied rules to the new local environment before implementing gameplay features.

Do not assume the original repository layout exists.

Do not assume the example mod idea is literal.

If the user says something like "create a transparent ground mod like X-Ray", treat X-Ray as an example of the kind of
feature they want, not as a command to copy an existing project. The same bootstrap and research workflow applies to
any requested first mod.

---

## Existing TimberbornMods Checkout Build/Release Bootstrap

A clean checkout of the existing TimberbornMods repository is not enough for build, real-game validation, or release
publishing. Several required paths, tools, credentials, generated references, and package outputs are intentionally
ignored by Git.

Do not assume these local items exist on a clean disk. When a required item is missing, explain what it is used for and
ask whether to locate it, create a link/config, generate it, or continue without that optional capability.

### Required Local Links

Normal build and release work may need both root aliases and dependency links:

| Path | Target | Used for |
| --- | --- | --- |
| `_GAME!` | Timberborn game installation root | game assets, modding archives, release/export scripts |
| `_WORKSHOP!` | Steam Workshop content folder for app id `1062090` | subscribed dependency mods and post-release artifact checks |
| `_MODS!` | user Timberborn mods folder | local package output, real-game validation, release ZIPs |
| `_LOGS!` | Timberborn LocalLow folder with logs/settings | runtime logs and local game diagnostics |
| `Dependencies/GameRoot` | Timberborn game installation root | C# project references to game assemblies |
| `Dependencies/Workshop` | Steam Workshop content folder for app id `1062090` | C# project references to subscribed dependency mods |

`_GAME!` and `Dependencies/GameRoot` usually point to the same game install, but both may be needed because scripts and
project files use different conventions. `_WORKSHOP!` and `Dependencies/Workshop` usually point to the same Workshop
folder for the same reason.

### External Tools And Accounts

Before build or release work, verify the tools required by the task:

- Git and PowerShell.
- A .NET SDK capable of restoring NuGet packages and building the changed projects.
- Unity Hub or Unity Editor matching `ModsUnityProject/ProjectSettings/ProjectVersion.txt`, or an explicit
  `-UnityPath` for Unity export.
- SteamCMD installed in a discoverable location or configured in `.tools/steam/steam.local.json`.
- Steam account login and Steam Guard readiness for uploads.
- GitHub CLI `gh` installed and authenticated for repository releases, issue comments, and issue closing.
- Mod.IO owner access token stored locally under `.tools/modio/` or supplied explicitly with `-AccessTokenPath`.

Secrets and account-specific configs must stay local and ignored. Do not copy, rename, guess, or commit token files.

### Ignored Local Configs And Generated Folders

The repository may create or require ignored local files and folders, including:

- `.tools/ilspy` for decompilation tooling.
- `.tools/steamcmd` if SteamCMD is installed under repo-local ignored tools.
- `.tools/steam/steam.local.json` for SteamCMD path/user configuration.
- `.tools/steam/publisher.key.txt` for Steam tag updater access.
- `.tools/modio/<ModName>.local.json` for Mod.IO API base, game id, and mod id.
- `.tools/modio/<ModName>.token.txt` or another explicit owner token file.
- `.tools/release-preview`, `.tools/release-staging`, `.tools/steam-staging`,
  `.tools/steam-tag-updates`, `.tools/steam-description-updates`, `.tools/github-release-notes`,
  `.tools/release-preflight`, and `.tools/unity-logs` as generated working/output folders.

Generated working folders should not be hand-authored except for the documented local config/secret files.

### Generated References

Generated references are optional until a task needs them:

- `_DecompiledGame/` is generated from game assemblies and used for architecture research.
- `_ExtractedGameAssets/` is generated from `_GAME!/Timberborn_Data/StreamingAssets/Modding/*.zip` and used for
  blueprints, localizations, shaders, and UI assets.

Treat generated references as read-only caches. If they are missing, stale, or incomplete, generate or refresh them
instead of designing around missing files.

### Release Package Local State

Release package sources and ZIPs are local artifacts, not tracked repository files.

For `LocalModFolder` releases, `_MODS!/<ModName>` must be materialized by Unity export and C# build before publishing.
On a clean disk it will not exist until preflight/build/export creates it.

Release ZIPs under `_MODS!` are generated artifacts. Historical ZIP archives may exist on a developer machine, but they
are local history and are not required for normal current release bootstrap unless a release config explicitly points at
one.

For `ExistingZip` release configs, verify that the configured ZIP exists and is fresh. If it is missing or stale, stop
and ask how to generate or provide it instead of publishing an old or guessed artifact.

### Platform Metadata And Wiki Checkout

Platform IDs and descriptions come from several places:

- Steam metadata may come from `_MODS!/<ModName>/workshop_data.json`, release config, or platform tooling.
- Mod.IO IDs come from `.tools/modio/<ModName>.local.json`.
- Platform description source files are tracked under each mod's `Workshop` directory, but live verification requires
  network access and platform credentials.

Wiki work requires a separate sibling checkout, normally `<repo-root>.wiki`, cloned from:

```text
https://github.com/ihsoft/TimberbornMods.wiki.git
```

Do not create Wiki pages inside the main repository.

### Useful Bootstrap Checks

For a clean checkout, verify only the capabilities needed by the current task:

- root aliases exist and point to plausible targets,
- `Dependencies/GameRoot` and `Dependencies/Workshop` exist,
- game managed assemblies exist under `Dependencies/GameRoot/Timberborn_Data/Managed`,
- Unity Editor version is installed/resolvable and the Unity project is not already open before batch export,
- SteamCMD path/user config exists if publishing to Steam,
- `gh auth status` succeeds if GitHub releases or issue work are needed,
- Mod.IO configs/tokens exist for the target mod or a shared explicit owner token path is available,
- `_MODS!/<ModName>` exists or can be generated for `LocalModFolder` releases,
- generated references exist only when the current research task needs them.

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

When something important is missing, do not merely report the missing item. Explain what it is used for and propose the
next setup action, such as locating an existing path, creating a local link, generating references, or skipping an
optional resource if the current task does not need it.

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

Common Windows target examples:

```text
_GAME!      -> <Steam library>\steamapps\common\Timberborn
_WORKSHOP!  -> <Steam library>\steamapps\workshop\content\1062090
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

When asking, include a short setup proposal. For example:

- If `_GAME!` is missing, explain that the Timberborn install path is needed for game assemblies, publicized
  references, and asset extraction, then ask whether to create a local link to the install folder.
- If `_MODS!` is missing, explain that the local mods folder is needed for real-game validation builds, then ask
  whether to create a local link to the user's Timberborn mods folder.
- If `_WORKSHOP!` or `_LOGS!` is missing, explain what the current task would use it for before asking to configure it.
- If the Wiki checkout is missing, explain that GitHub Wiki pages live in a separate repository and ask whether to
  clone `https://github.com/ihsoft/TimberbornMods.wiki.git` into sibling path `<repo-root>.wiki`, locate an existing
  checkout, or continue without Wiki edits.

After the user answers, set up the repository and continue with the normal Timberborn modding workflow.
