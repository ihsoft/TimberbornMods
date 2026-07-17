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

Derive required local aliases from current tracked project references and this bootstrap documentation, not merely
from suggestive names of existing or broken local links. For an unexpected link with no current tracked reference or
documented purpose, do not guess or recreate its target. Report the evidence and ask whether to remove it.

### External Tools And Accounts

Before build or release work, verify the tools required by the task:

- Git and PowerShell.
- A .NET SDK capable of restoring NuGet packages and building the changed projects.
- Unity Hub signed in with a valid Editor license/entitlement, plus the Unity Editor matching
  `ModsUnityProject/ProjectSettings/ProjectVersion.txt` or an explicit `-UnityPath` for Unity export.
- SteamCMD installed in a discoverable location or configured in `.tools/steam/steam.local.json`.
- Steam account login and Steam Guard readiness for uploads.
- GitHub CLI `gh` installed and authenticated for repository releases, issue comments, and issue closing.
- Mod.IO owner access token stored locally under `.tools/modio/` or supplied explicitly with `-AccessTokenPath`.

Secrets and account-specific configs must stay local and ignored. Do not copy, rename, guess, or commit token files.

Before the first interactive or batch Unity validation on a clean machine, verify Hub sign-in and Editor licensing.
If licensing is not ready, explain that the user must complete the interactive Unity Hub sign-in/license step before
batch compilation or export; do not diagnose the resulting startup failure as a project-code problem.

### Unity Modding Tools First Open

A clean `ModsUnityProject` checkout needs the official Timberborn Modding Tools first-open workflow before batch
compilation or export. Installing the Unity Editor alone does not import the game DLLs and assets required by the
project.

Follow the current official [Unity setup](https://github.com/mechanistry/timberborn-modding/wiki/Unity-setup) and
[Asset importer](https://github.com/mechanistry/timberborn-modding/wiki/Asset-importer) instructions. At minimum:

1. Install the exact Unity Editor version from `ModsUnityProject/ProjectSettings/ProjectVersion.txt` with both Windows
   Build Support and Mac Build Support.
2. Add `ModsUnityProject` to Unity Hub without opening it, then add `-disable-assembly-updater` to that project's Hub
   command-line arguments.
3. Open the project interactively. If Unity offers Safe Mode, choose `Ignore` so the Timberborn Modding Tools can run.
4. Complete the Timberborn library import prompt using the verified game installation root. If the prompt does not
   appear, use `Timberborn -> Import Timberborn DLLs and assets` from the Unity toolbar.
5. Wait for the import and Unity compilation to finish. Verify that `ModsUnityProject/Assets/Plugins/Timberborn`
   exists and contains the imported/publicized game DLLs before attempting batch compilation or export.

After the first import or any version-driven refresh, follow
`docs/agent-knowledge/Timberborn-Unity-Import-Operational-Knowledge-v1.md` and pass its package-dependency,
assembly-load, and custom-type gates before batch compilation or export.

The importer also refreshes dynamic, game-version-specific assets under
`ModsUnityProject/Assets/Tools/ImportedAssets`. Keep that output local and ignored, and repeat the import after game
updates when the official tool requires it.

This repository already tracks the Timberborn Modding Tools under `ModsUnityProject/Assets/Tools`. Do not also install
the tools from their Git package URL when that directory and its `package.json` are present; that is the alternative
setup for a Unity project that does not already contain the tools.

#### ImportedAssets Lifecycle

`ModsUnityProject/Assets/Tools/ImportedAssets` is dynamic, game-version-specific output owned by the official
Timberborn importer. `ModsUnityProject/.gitignore` intentionally ignores it. Keep it local and never stage or commit
it, including with `git add -f`. Re-run the importer after relevant game updates instead of storing a snapshot in Git.

Do not treat legacy tracked files or historical refresh commits as evidence that a tracked snapshot is intended. If
legacy tracked files exist, get explicit user approval for a dedicated cleanup, then remove only this path from the
Git index while preserving the local importer output:

```powershell
git rm -r --cached -- ModsUnityProject/Assets/Tools/ImportedAssets
```

Before committing that cleanup, verify that:

- `git ls-files -- ModsUnityProject/Assets/Tools/ImportedAssets` returns no tracked paths,
- the local `ImportedAssets` files still exist,
- `git ls-files --others --ignored --exclude-standard -- ModsUnityProject/Assets/Tools/ImportedAssets` reports the
  local output as ignored,
- the staged diff contains the expected index deletions and no path outside the explicitly approved cleanup scope.

### Ignored Local Configs And Generated Folders

The repository may create or require ignored local files and folders, including:

- `.agents/enable-ada` as the local opt-in marker for the optional Ada Personality profile.
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

### Role Agent Tasks

Bootstrap the repository's dedicated role agents in Codex or the equivalent agent environment. These role tasks are
part of the working repository setup because coders must be able to delegate rule, publishing, and Wiki work without
changing roles themselves.

1. Search for an existing task for each dedicated role before creating one.
2. Verify that a match belongs to the current TimberbornMods repository and has the expected role.
3. Create each missing role task when the environment provides task-creation tools and the user has authorized task
   creation. Otherwise, ask the user to create it or provide the existing task contact.
4. Give role tasks stable, searchable titles:
   - `TimberbornMods Mentor`
   - `TimberbornMods Publisher`
   - `TimberbornMods Wiki editor`
5. Initialize each new role task with its primary responsibility, role boundaries, and required instruction files.
6. Tell the new role agent to read those files, summarize its mandate and safety gates, make no repository or external
   changes, and wait for a concrete assignment.
7. Confirm that each task can be found by the role name alone and by the repository-and-role title.

Use English only when creating an agent task. The task title, initialization prompt, role mandate, boundaries, and all
other text sent as part of agent creation must be in English, even when the current user conversation uses another
language. Do not mix languages in the creation or initialization message.

Use the current repository rules as the source of truth for each initialization prompt. At minimum, initialize the
roles as follows:

| Role | Primary responsibility | Required role instructions |
| --- | --- | --- |
| Mentor | Own and organize agent rules, evaluate delegated rule suggestions, and improve future decision quality. | `AGENTS.md` and the rule files relevant to the requested rules-maintenance scope. |
| Publisher | Safely prepare, validate, package, and publish explicitly approved mod releases, including post-release workflow. | `AGENTS.md` and `docs/timberborn-release-publishing-rules-for-ai-agents.md`. |
| Wiki editor | Maintain accurate player-facing documentation in the separate GitHub Wiki checkout based on confirmed behavior and released capabilities. | `AGENTS.md`, `docs/agent-knowledge/Wiki-Operational-Knowledge-v1.md`, and `docs/timberborn-wiki-editing-rules-for-ai-agents.md`. |

The initialization prompt must state that the role does not own adjacent work. In particular, the Publisher must not
implement unrelated code, edit agent rules, or edit the Wiki; the Wiki editor must not implement mod code, publish
releases, or edit agent rules; and the Mentor must not implement mod code, publish releases, or edit the Wiki unless
the user explicitly expands that role for the current task. External publication and player-facing Wiki changes
require a concrete user assignment and all applicable safety gates.

Do not create duplicate role tasks when a clear matching task already exists. Do not treat a narrowly worded search
with no results as proof that the role task is missing; fall back to searching by the role name alone.

### Optional Ada Personality Profile

The Ada Personality profile is a tracked, optional communication preset. It uses Russian by default, refers to the
agent in the feminine form and the user in the masculine form, and favors calm, conversational communication without
flattery or exaggerated enthusiasm. It changes communication style only; it does not replace engineering,
repository, role, safety, or task-specific rules.

During interactive bootstrap, when `.agents/enable-ada` does not exist, explicitly ask:

> Enable the optional Ada Personality profile for this checkout? It uses Russian by default, refers to the agent in
> the feminine form and to the user in the masculine form, and applies a calm, conversational style without flattery
> or exaggerated enthusiasm. It changes communication style only, not engineering or repository rules.

- If the user opts in, create the local `.agents/enable-ada` marker.
- If the user declines, do not create the marker.
- If bootstrap is noninteractive or the user does not answer, leave the profile disabled and report that choice.
- Do not infer consent from the language of the conversation.
- If the marker already exists, report that Ada is enabled and preserve the marker unless the user explicitly asks to
  disable the profile.

The marker is a local preference and must remain ignored. Do not commit it.

### Useful Bootstrap Checks

For a clean checkout, verify only the capabilities needed by the current task:

- root aliases exist and point to plausible targets,
- `Dependencies/GameRoot` and `Dependencies/Workshop` exist,
- game managed assemblies exist under `Dependencies/GameRoot/Timberborn_Data/Managed`,
- Unity Hub sign-in and Editor licensing are ready, and the required Editor version and both Build Support modules are
  installed/resolvable,
- the official Unity first-open import has populated `ModsUnityProject/Assets/Plugins/Timberborn`, the Unity Import
  Operational Knowledge gates pass before batch compilation or export, and the Unity project is not already open before
  batch export,
- SteamCMD path/user config exists if publishing to Steam,
- `gh auth status` succeeds if GitHub releases or issue work are needed,
- Mod.IO configs/tokens exist for the target mod or a shared explicit owner token path is available,
- `_MODS!/<ModName>` exists or can be generated for `LocalModFolder` releases,
- generated references exist only when the current research task needs them.
- dedicated `Mentor`, `Publisher`, and `Wiki editor` tasks exist and have stable searchable titles when the agent
  environment supports persistent role tasks.

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
   - existing examples,
   - dedicated role agent tasks.
5. Resolve the optional Ada Personality choice as described above.
6. Ask the user for local paths that cannot be discovered safely.

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
.agents/enable-ada
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

- Should the optional Ada Personality profile be enabled for this checkout? Explain its language, grammatical-gender,
  and communication-style behavior before asking.
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
