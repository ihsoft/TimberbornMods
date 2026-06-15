# Timberborn Release Publishing Rules for AI Agents

Use these rules when preparing, validating, or publishing Timberborn mod releases to Steam Workshop or Mod.IO.

Publishing is a high-risk workflow. Prefer stopping and asking over guessing.

## Explicit publish requests only

Never publish to Steam or Mod.IO unless the user explicitly asks to publish.

Dry runs may build packages, validate files, generate metadata, and prepare staging directories. They must not upload
anything.

## Exact version matching

When the user names a release version, it must exactly match the configured release version.

Examples:

- If the user asks for `Automation 1.4.0` and the release config says `4.1.0`, stop.
- If the user asks for `Automation 4.1` and the release config says `4.1.0`, stop.

Do not treat close versions as aliases. Ask the user to confirm the exact version.

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

Before publishing, compare the local `Workshop` description files with the current descriptions published on Steam
Workshop and Mod.IO when platform access is available.

If a local description and the published platform description differ, stop. Do not publish until the user decides which
side is correct:

- update the platform from the local `Workshop` file,
- update the local `Workshop` file from the platform,
- or manually reconcile the difference.

Do not silently overwrite either side when a mismatch is found.

When a release changes user-visible behavior, update the platform description as the current product description for a
new player. Describe what the mod does now. Do not describe removed or historical behavior in the main platform
description. Put historical change information only in changelog or release notes when needed.

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

## Stop on uncertainty

Stop and ask if any of these are unclear or inconsistent:

- requested mod name,
- requested version,
- package source path,
- final package contents,
- `version-X.X` folders,
- manifest versions,
- DLL/XML names,
- Steam `publishedfileid`,
- Mod.IO game/mod IDs,
- Steam or Mod.IO credentials.
