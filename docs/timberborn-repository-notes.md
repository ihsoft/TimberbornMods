# Timberborn Repository Notes

## Purpose

This document contains repository-specific knowledge that is useful when working with the TimberbornMods repository.

Unlike the general modding guides, the contents of this document are specific to this repository and its development practices.

Package-specific build, export, real-game output, and focused test selection live in
`docs/agent-knowledge/Timberborn-Repository-Validation-Operational-Knowledge-v1.md`.

---

## Repository Structure

This repository contains multiple Timberborn mods and supporting libraries.

Examples include:

- Automation
- AutomationForModdableWeather
- TimberCommons
- XRay
- SmartPower

Do not assume that all mods use identical project layouts.

Always inspect the specific mod before making changes.

## Mod-Specific Instruction Files

Mod-specific `AGENTS.md` files are welcome when a rule applies only to one mod. Treat the root `AGENTS.md` as the
repository-wide baseline, then apply the closest mod-specific `AGENTS.md` for files in that mod.

Good candidates for a mod-specific `AGENTS.md`:

- exact package-data paths,
- known localization files,
- mod-specific test commands,
- release or package quirks,
- compatibility notes for public APIs,
- known game-version or lifecycle pitfalls.

Do not put these details in the root `AGENTS.md` unless they apply repository-wide.

---

## ModsUnityProject

Use `docs/agent-knowledge/Timberborn-Unity-Operational-Knowledge-v1.md` for the artifact model and pipeline selection.

In this repository, Unity-owned package data commonly lives under `ModsUnityProject/Assets/Mods/<ModName>`, not beside
the C# project. Check the package matrix for the actual owner and split layouts before editing localization, blueprints,
UI assets, thumbnails, workshop metadata, or bundles.

When Unity-owned data changes, export the correct compatibility lane through `tools/export-unity-mod.ps1` /
`ModBuilderBatch` before real-game validation. Never substitute a manual copy into `_MODS!`. A C# build normally
refreshes only script output; if Unity export was not run, report that the local package was not refreshed.

---

## Project Roles

### TimberCommons

TimberCommons is a regular player-facing Timberborn mod.

It is named "Commons" because it contains many small gameplay and UI changes, not because it is a general
shared library.

One important exception is the irrigation tower system. TimberCommons provides reusable components based on
`IrrigationTower`, such as `GoodConsumingIrrigationTower` and `ManufactoryIrrigationTower`, that can be used by
other mods, including third-party mods.

When modifying those irrigation tower components or their specs, consider compatibility for external mods that may
depend on them. Do not treat unrelated TimberCommons features as shared infrastructure unless there is evidence.

### TimberDev

Developer-facing shared functionality.

Contains utilities intended to support mod development.

Shared functionality that is not player-facing may belong here instead of TimberCommons.

TimberDev is a standalone source scope for ownership and validation, but the current checkout has no
`TimberDev.csproj`; its files are linked into consumer projects. When working on TimberDev, treat its code as depending
only on TimberDev and the game APIs. Do not use other mods as a TimberDev validation gate by default.

If a TimberDev change affects logic that another mod actually uses, also run that mod's tests as downstream regression
coverage.

## Local Tools and Generated Game References

The repository distinguishes tracked helper scripts from local/generated artifacts:

- `tools/` contains repository scripts and helper commands. These files are intended to be tracked in Git.
- `.tools/` contains locally installed external tools, such as `ilspycmd`. This directory is machine-local and ignored.
- `_DecompiledGame/` contains generated decompiled Timberborn game sources. This directory is ignored.
- `_ExtractedGameAssets/` contains generated extracted game modding assets. This directory is ignored.

Use decompiled game sources as a read-only architecture reference.

Use extracted game assets as read-only data and UI references.

Before using generated references as evidence, identify the target installed Timberborn build for the task and verify
the cache's `generation-provenance.json`. Both `_DecompiledGame/` and `_ExtractedGameAssets/` use this file at their
output root.

Schema v1 records:

- `SchemaVersion`, `ResourceKind`, and `GeneratedAtUtc`;
- `Game.CurrentVersion` from `StreamingAssets/VersionNumbers.json` and `Game.VersionText` from
  `StreamingAssets/Version.txt`;
- the repository-relative generator script name and its SHA-256, plus the external tool name/version when applicable;
- effective generation options;
- the portable source-relative path, byte length, and SHA-256 of every assembly or archive in the generated input set.

The normal verification path compares both recorded `Game` values with the target installation and confirms that the
resources needed by the task are listed in `Inputs`. When the game identity matches exactly and the required resources
are covered, treat those generated resources as verified without recomputing every source-input hash.

The recorded lengths and SHA-256 values preserve what the generator actually consumed and support targeted provenance
checks. Recompute them only for the specific inputs whose identity is uncertain, when the installation may have been
manually modified or partially updated, or when diagnosing cache integrity. Do not scan every input hash during routine
generated-reference use.

The manifest proves only the exact input set it lists. Outputs left from another run but absent from `Inputs` remain
unverified. Do not infer equivalence from directory or file timestamps. Generator identity is audit evidence, and a
changed generator requires regeneration when the task depends on the changed generation semantics.

If provenance is absent, incomplete, invalid, has a different game identity, does not cover a resource needed by the
task, or fails a targeted input check, treat the cache as unverified and regenerate it before drawing conclusions. A
failed or partial generation must not leave provenance claiming that the cache is current.

Provenance must remain portable. Do not store absolute installation paths, local aliases, usernames, hostnames, or
other machine-specific identity in it.

Treat `_ExtractedGameAssets` as a generated cache that can be stale or partially extracted. When an expected current
game UI or data asset is missing there, check the source archive under
`_GAME!/Timberborn_Data/StreamingAssets/Modding/*.zip` or rerun the extraction script before concluding that the asset
does not exist.

Do not edit game DLLs.

Do not edit generated files under `_DecompiledGame/`.

Do not edit generated files under `_ExtractedGameAssets/`.

Regenerate `_DecompiledGame/` from the game assemblies when provenance verification requires it.

Regenerate `_ExtractedGameAssets/` from the game modding archives when provenance verification requires it.

The game modding archives are located under:

```text
_GAME!/Timberborn_Data/StreamingAssets/Modding/
```

Important archives:

- `Blueprints.zip` contains game blueprints.
- `Localizations.zip` contains game localization files.
- `Shaders.zip` contains game shaders.
- `UI.zip` contains game UI assets, including UXML, USS, and sprites.

Use:

```powershell
tools/extract-game-modding-assets.ps1
```

to extract them into:

```text
_ExtractedGameAssets/
```

The extraction script supports PowerShell `-WhatIf`; use it when checking archive paths without rewriting generated
asset folders.

---

## Wiki Documentation

The GitHub Wiki may contain user-facing and modder-facing documentation for public mod APIs and workflows.

When changing behavior, public components, blueprint specs, modder-facing APIs, workshop-visible features, or
documented workflows, check whether the Wiki needs an update.

For TimberCommons irrigation tower components, check:

```text
https://github.com/ihsoft/TimberbornMods/wiki/Timber-Commons
```

Do not update Wiki pages for internal-only refactoring unless public behavior or documented API changes.

---

## Package Changelogs

When completing a user-visible feature or bug fix for a mod package, update that package's `CHANGELOG.md` before
committing. Do not add noisy changelog entries for internal refactors, test-only changes, or documentation-only changes
unless they have user-visible release-note value or the user asks for it.

Keep entries short. For features, describe the new capability. For fixes, describe the broken behavior before the fix.

Group implementation controls, compatibility switches, and small options under the primary user-facing feature unless
they are independently meaningful to players as a separate change. Changelog bullets should describe delivered
user-facing feature slices, not every implementation piece added inside one feature.

Do not manually wrap changelog bullet text to the 120-character code line limit. Keep each changelog bullet as one
logical line even when it is long; downstream platform and UI renderers are responsible for visual wrapping. Use
continuation lines only when the changelog intentionally needs a separate paragraph or list item.

If the work corresponds to a GitHub issue, include the issue number in the bracket prefix:

```text
* [Feature #83] Add breeding pod progress signal.
* [Fix #123] Game could crash when opening the panel.
```

If the changelog has no top section marked `(TBD)`, start one using this heading format:

```text
# v4.4.0 (TBD)
```

Treat changelog sections with a concrete release date as published history.

Do not add new entries to dated changelog sections unless the user explicitly asks to correct historical release notes.

If the top changelog section is dated, new user-visible changes must go into a new `(TBD)` section above it.

Choose the next version from the last published version of that package:

- feature work starts the next minor version,
- fix-only work starts the next patch version.

If an existing `(TBD)` section was started as a patch version and a feature is added before publication, rename the
pending section to the next minor version because feature scope dominates patch scope.

Before committing any player-visible change, perform a semantic changelog review rather than checking only that an
entry exists. Compare every new entry category with the top `(TBD)` heading and the package's last dated version, state
the expected pending package version in the final review, and correct the heading before commit when the categories do
not match it. A pending section containing any `[Feature]` entry must use the next minor version; a fix-only pending
section uses the next patch version.

Each package may have its own changelog and version stream. Update the target package changelog, not a repository-wide
changelog, unless the task explicitly affects repository-wide release notes.

During ordinary code compatibility work after a Timberborn update, do not update release metadata such as
`release.json` or `directory.build.props` unless the user explicitly asks for release preparation. A changelog `(TBD)`
section, Unity manifest minimum game version, and code changes may be correct for compatibility work while release
metadata remains owned by the later publish or release workflow.

For compatibility updates after a Timberborn game update, prefer a single `[Update] Support game version X.Y.Z...`
entry when the work is primarily restoring support for the new game version. Include important adapted stock behavior
in that update entry. Do not split it into a separate `[Fix]` unless players could encounter the broken behavior in a
released mod version.

## Temporary Compatibility Code

When adding temporary save, script, data, or package compatibility code, mark it with a clear dated removal comment.
The comment should say what legacy behavior is being supported and when the path should be removed or reconsidered.

Use a concrete date or release window, not an open-ended note such as "remove later". Give players enough migration time
for saved games, scripts, or data that may already exist in the wild.

During maintenance or release preparation for the affected mod, scan for expired temporary compatibility comments. If a
temporary path has expired, either remove it or deliberately renew the date with a short justification. Do not let
temporary compatibility paths become permanent by forgetting their removal window.

---

## GitHub Issue References in Commits

For commits that implement or fix a GitHub issue, link the issue in the commit body with:

```text
Refs #83
```

Do not use auto-closing keywords such as `Closes #83`, `Fixes #83`, or `Resolves #83` in ordinary implementation
commits.

In this repository, code may be committed before the mod is published to players. Auto-closing the issue at commit,
push, or merge time can make the public issue state misleading.

---

## Publicizer

Before using reflection or AccessTools:

1. Check the project file.
2. Check whether assemblies are publicized.

If direct access is available, prefer direct access.

Publicized direct access to private or internal game members is an accepted repository practice when public APIs do not
cover the need. Do not introduce reflection, Harmony `AccessTools`, or local reimplementations unnecessarily only to
avoid publicized access.

---

## Harmony

Harmony is not the default solution.

Prefer:

- dependency injection,
- existing services,
- existing extension points,
- component registration,
- configurator-based integration.

Use Harmony only when necessary.

---

## Dependency Injection

Do not assume that ILoadableSingleton.Load() is the earliest safe initialization point.

Some Harmony patches and UI systems may execute before Load().

When a bridge between DI and static code is required:

- constructor initialization may be preferable,
- verify actual execution order before relying on Load().

---

## Branches

Inspect the repository before assuming branch structure.

Historically, release branches may exist for specific Timberborn versions.

Do not assume that main is always the only active branch.
