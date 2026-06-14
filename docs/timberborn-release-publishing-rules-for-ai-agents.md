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
