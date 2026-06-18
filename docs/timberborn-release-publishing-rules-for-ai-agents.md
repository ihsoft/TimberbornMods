# Timberborn Release Publishing Rules for AI Agents

Use these rules when preparing, validating, or publishing Timberborn mod releases to Steam Workshop or Mod.IO.

Publishing is a high-risk workflow. Prefer stopping and asking over guessing.

## Explicit publish requests only

Never publish to Steam or Mod.IO unless the user explicitly asks to publish.

Dry runs may build packages, validate files, generate metadata, and prepare staging directories. They must not upload
anything.

## Dirty worktree before publishing

Before uploading a mod release, check for uncommitted changes that belong to the target mod or to shared
release-critical dependencies.

Uncommitted changes in unrelated mods do not block publishing.

For the target mod, treat these uncommitted changes as a red flag:

- files under the target mod directory,
- the target mod's test project,
- `ModsUnityProject/Assets/Mods/<ModName>/`,
- the target mod's `Workshop` files, changelog, release metadata, and package source tracked in Git,
- release scripts, release configs, or shared publishing tools changed for the current release.

For any mod release, also treat uncommitted changes in `TimberDev` or `TimberDev.Tests` as a red flag because
TimberDev is shared by many mods.

When red-flag changes are present:

1. Stop before upload.
2. List the specific files or file groups.
3. Ask the user one direct question: whether to continue publishing despite those uncommitted changes.
4. Do not fix, stage, commit, revert, or otherwise clean up those changes as part of the publish flow unless the user
   explicitly asks.

Uncommitted changes in unrelated mods may be mentioned as background, but they should not block publishing.

For shared docs/tools changes, block only when they affect the current publish flow, release validation, release
metadata, package construction, or platform upload. Otherwise, mention them as background instead of stopping.

## Exact version matching

When the user names a release version, it must exactly match the configured release version.

Examples:

- If the user asks for `Automation 1.4.0` and the release config says `4.1.0`, stop.
- If the user asks for `Automation 4.1` and the release config says `4.1.0`, stop.

Do not treat close versions as aliases. Ask the user to confirm the exact version.

Before release preparation, verify that the target package changelog has a matching version section and user-visible
release notes. Changelog workflow rules live in `docs/timberborn-repository-notes.md`.

## Exact source paths only

If `release.json` points to `Package.SourcePath`, use only that exact path.

If it does not exist, stop. Do not:

- search for similar folders,
- use `*-bak` folders,
- substitute previous ZIP files,
- infer a different source folder from timestamps or names.

Tell the user to restore the exact source path, rebuild/export the mod to the exact path, or explicitly update
`release.json`.

## Source folder is truth

For `Package.Mode = "LocalModFolder"`, the source folder is the source of truth.

The configured source folder is the only input for release package contents. Agents may validate it, package it, and
report what is missing. Agents must not repair, supplement, or reinterpret it using previous archives, backup folders,
similarly named folders, or inferred state.

If the source folder does not contain a game-version folder, the release does not contain it.

Before building a release ZIP from `Package.SourcePath`, make sure the current code has been built into that exact
source folder. For C# mods, run the mod project build with the real `ModPath` and verify that the expected DLL/XML
files in the source folder were updated. Do not package a local mod folder that may contain stale binaries.

Before building the release binary, update the mod's `directory.build.props`. The DLL assembly version is taken from
that file. Verify the built DLL assembly version after the build. Other version declarations still matter for their own
consumers: the Unity `manifest.json` version is used by the game, and `release.json` is used by the release process.

If `directory.build.props` was updated after a build or package dry run, treat the previous DLL and ZIP as stale and
rebuild/repackage before publishing.

If the release package is built from a local Unity-exported mod folder, ask the user to confirm that Unity assets were
exported before building the ZIP. Do this before packaging, because missing or stale asset bundle changes cannot be
detected reliably from C# build output.

## Package validation

Before any upload, validate the final package, not just source files.

Every package must contain at least one `version-X.X` folder.

Every `version-X.X` folder must contain:

- `manifest.json`,
- `Scripts/<ScriptFileBase>.dll`,
- `Scripts/<ScriptFileBase>.xml`.

If `ManifestVersions` is configured, each manifest version must match its configured version for that folder.

## Steam game-version coverage

Steam cannot target Timberborn Main and Experimental branches separately.

For Steam, if a package contains only one `version-X.X` folder, stop unless `Steam.AllowSingleGameVersion` is explicitly
configured with a `Steam.CompatibilityReason`.

Do not infer compatibility across major Timberborn versions.

## Mod.IO compatibility suffix

For Mod.IO, generate the game compatibility suffix from the actual `version-X.X` folders in the final package.

Example:

```text
---
MinimumGameVersion: 1.0.12.7
MaximumGameVersion: 1.1.99.99
---
```

Do not claim support for a game version unless the final package contains the corresponding `version-X.X` folder.

## Platform descriptions

Local files under a mod's `Workshop` directory are the expected source for published platform descriptions.

Do not apply these rules to mods explicitly known as dead, unpublished, or kept only for reference. In this repository,
`TimberUI` is a dead mod kept only for reference; it does not build and must be excluded from release and platform
description synchronization checks.

Before publishing any mod, compare the local `Workshop` description files with the current descriptions published on
Steam Workshop and Mod.IO when platform access is available. This synchronization verification is mandatory for every
release.

Use `tools/verify-platform-descriptions.ps1` for this check when possible.

If a local description and the published platform description differ, stop. Do not publish until the user decides which
side is correct:

- update the platform from the local `Workshop` file,
- update the local `Workshop` file from the platform,
- or manually reconcile the difference.

Do not silently overwrite either side when a mismatch is found.

Updating platform descriptions is not part of the normal release flow. Change local description files or published
platform descriptions only when the user explicitly asks for a description update.

When the user asks to update a description because release behavior changed, update it as the current product
description for a new player. Describe what the mod does now. Do not describe removed or historical behavior in the
main platform description. Put historical change information only in changelog or release notes when needed.

Preserve each platform's existing markup and file style. Steam Workshop descriptions use Steam formatting:

```text
https://steamcommunity.com/comment/Recommendation/formattinghelp
```

Example:

```text
[h1]Header[/h1]
[h2]Section[/h2]
[list]
[*]Item
[/list]
```

Mod.IO descriptions use regular HTML formatting, such as:

```html
<h1>Header</h1>
<h2>Section</h2>
<ul>
  <li>Item</li>
</ul>
```

Make the smallest description edit that matches the current mod behavior. Do not regenerate or restyle the full
description unless the user explicitly asks for it.

Steam descriptions must be compared as exact text after normalizing CRLF/LF line endings only. Do not ignore trailing
spaces, blank lines, line breaks, punctuation, or other formatting characters in Steam descriptions. Steam formatting is
plain text markup, so whitespace may change rendering.

Mod.IO descriptions use HTML and the platform may normalize HTML attributes, links, entities, and whitespace. For
Mod.IO synchronization, compare the visible rendered text after HTML decoding and tag stripping instead of requiring
byte-for-byte HTML equality.

An existing Mod.IO token for one mod can be used to read descriptions for the other mods owned by the same account and
game scope. If the available tokens cannot read a mod description, stop and ask the user to create or provide a new
token.

When the user explicitly asks to update a Steam description, prefer `tools/update-steam-description.ps1`. The script
does a dry run by default, updates only when `-Publish` is passed, and verifies the live Steam description after upload.
Do not hand-write Steam description VDFs unless the script is unavailable and the user explicitly approves the risk.
SteamCMD description VDFs are fragile: unescaped double quotes inside multiline `description` values can truncate the
published description. If a Steam description contains double quotes, replace them or improve and test the escaping
before publishing.

## Steam change notes

Steam change notes use Steam formatting.

Use:

```text
[h3]vX.Y.Z[/h3]
[*] Change item.
```

Convert changelog bullet lines from:

```text
* [Update] Something changed.
```

to:

```text
[*] [Update] Something changed.
```

## Steam login retry

Steam Guard mobile confirmation may take longer than an agent command timeout.

Use a login-only/retry mode that opens SteamCMD for interactive login and does not build, stage, or publish anything.

Do not store Steam passwords in repository files or local config files.

## Closing GitHub issues after release

When publishing a mod release, identify the issue-backed changes included in that release.

Before closing any GitHub issues, show the user the exact list of issues proposed for closure and ask for explicit
confirmation.

Never close GitHub issues automatically as a side effect of committing, pushing, packaging, or publishing.

If release scope is unclear, ask instead of closing issues.

## Stop on uncertainty

Stop and ask if any of these are unclear or inconsistent:

- requested mod name,
- requested version,
- package source path,
- final package contents,
- `version-X.X` folders,
- manifest versions,
- DLL/XML names,
- platform description synchronization,
- Steam `publishedfileid`,
- Mod.IO game/mod IDs,
- Steam or Mod.IO credentials.
