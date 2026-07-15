# Timberborn Repository Notes

## Purpose

This document contains repository-specific knowledge that is useful when working with the TimberbornMods repository.

Unlike the general modding guides, the contents of this document are specific to this repository and its development practices.

---

## Repository Structure

This repository contains multiple Timberborn mods and supporting libraries.

Examples include:

- Automation
- AutomationForModdableWeather
- TimberCommons
- TimberUI
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

### TimberUI

TimberUI is a dead mod kept only for reference. It does not build and should be excluded from release, publishing, and
platform description synchronization workflows unless the user explicitly asks to revive or inspect it.

## Package Build And Validation Matrix

Use this matrix to select the known project, focused test runner, package-data owner, and local instructions. Update it
when a production project, test project, package-data location, or local `AGENTS.md` is added, removed, or renamed.
Adding a new mod is not complete until its row records the known production project, focused test status, package-data
ownership, and any local instructions.

The focused test projects are executable console runners. Run them with `dotnet run --project`, not `dotnet test`.
`No focused tests` means that no package-specific test project exists in the current repository; it does not mean that
testing is unnecessary or that an unrelated package's tests should be used instead.

| Scope | Production project | Focused validation after real-game confirmation | Package data | Extra instructions |
|---|---|---|---|---|
| Automation | `Automation/Automation.csproj` | `dotnet run --project Automation.Tests/Automation.Tests.csproj` | `ModsUnityProject/Assets/Mods/Automation` | `Automation/AGENTS.md` |
| AutomationForModdableWeather | `AutomationForModdableWeather/AutomationForModdableWeather.csproj` | No focused tests | Project-local manifest and content | None |
| CustomTools | `CustomTools/CustomTools.csproj` | `dotnet run --project CustomTools.Tests/CustomTools.Tests.csproj` | `ModsUnityProject/Assets/Mods/CustomTools` | None |
| SmartHaulers | `SmartHaulers/SmartHaulers.csproj` | No focused tests | Split between `SmartHaulers/Mod` and `ModsUnityProject/Assets/Mods/SmartHaulers` | `SmartHaulers/AGENTS.md` |
| SmartPower | `SmartPower/SmartPower.csproj` | `dotnet run --project SmartPower.Tests/SmartPower.Tests.csproj` | `ModsUnityProject/Assets/Mods/SmartPower` | None |
| TimberCommons | `TimberCommons/TimberCommons.csproj` | `dotnet run --project TimberCommons.Tests/TimberCommons.Tests.csproj` | `ModsUnityProject/Assets/Mods/TimberCommons` | `docs/TimberCommons-modding-notes-for-ai-agents.md` |
| TimberDev source | No standalone production project in the current checkout | `dotnet run --project TimberDev.Tests/TimberDev.Tests.csproj` | No standalone mod package | This document |
| XRay | `XRay/XRay.csproj` | `dotnet run --project XRay.Tests/XRay.Tests.csproj` | `ModsUnityProject/Assets/Mods/XRay` | `XRay/AGENTS.md` |
| TimberUI | Reference only; do not build by default | None | `ModsUnityProject/Assets/Mods/TimberUI` | See `Project Roles` above |

`CustomResources`, `TestParser`, `TestSupport`, `UnityDevLite`, and projects under `tools/` are not active mod packages
in this matrix. They have support, source-sharing, legacy, or tooling roles. Inspect the specific project and task before
applying a mod build, test, export, or release workflow to one of them.

### Compile-only validation

For a changed production mod project, use the existing compile-only pattern so a post-build copy cannot turn a
successful compilation into a misleading local-package failure:

```powershell
dotnet build <ProjectPath>.csproj /p:ModPath=__NoSuchModPath__
```

This validates compilation only. It does not prepare a package for real-game testing.

### Real-game package output

For a code-only change, resolve the current `_MODS!` alias and pass its absolute target as `ModPath` unless the project
has already been verified to use that same destination. Do not rely on a project file's historical machine-specific
default.

For a change under `ModsUnityProject/Assets/Mods/<ModName>/`, use the Unity export path described in `ModsUnityProject`
above. For SmartHaulers, inspect both package-data locations from the matrix and use the export/build paths required by
the files changed.

After the package has been updated, verify the expected output artifact or timestamp before asking the user to test in
the game. Follow the root real-game validation gate before creating, changing, or running regression tests.

### Known downstream relationships

`AutomationForModdableWeather` has a project reference to `Automation`. When an Automation change affects the public
extension contract used by that plugin, also build `AutomationForModdableWeather` as downstream compile coverage.

TimberDev files are linked directly into multiple production and test projects. Run `TimberDev.Tests` for TimberDev
changes, then run or build only the downstream packages that actually include the changed shared file or behavior.

---

## Local Tools and Generated Game References

The repository distinguishes tracked helper scripts from local/generated artifacts:

- `tools/` contains repository scripts and helper commands. These files are intended to be tracked in Git.
- `.tools/` contains locally installed external tools, such as `ilspycmd`. This directory is machine-local and ignored.
- `_DecompiledGame/` contains generated decompiled Timberborn game sources. This directory is ignored.
- `_ExtractedGameAssets/` contains generated extracted game modding assets. This directory is ignored.

Use decompiled game sources as a read-only architecture reference.

Use extracted game assets as read-only data and UI references.

Before treating generated references as authoritative, verify that they match the target Timberborn game version or
branch for the task. Check available version markers, generated folder provenance, game assemblies, package
`MinimumGameVersion`, and the user's requested Stable or Experimental target. If the generated reference version does
not match, say so and avoid relying on it without additional verification against the correct game files.

Treat `_ExtractedGameAssets` as a generated cache that can be stale or partially extracted. When an expected current
game UI or data asset is missing there, check the source archive under
`_GAME!/Timberborn_Data/StreamingAssets/Modding/*.zip` or rerun the extraction script before concluding that the asset
does not exist.

Do not edit game DLLs.

Do not edit generated files under `_DecompiledGame/`.

Do not edit generated files under `_ExtractedGameAssets/`.

Regenerate `_DecompiledGame/` from the game assemblies when needed.

Regenerate `_ExtractedGameAssets/` from the game modding archives when needed.

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

## Localization

Localization files are typically stored as text files containing CSV content.

Expected columns:

ID,Text,Comment

Rules:

- Keep existing IDs unchanged.
- Preserve placeholders.
- Preserve formatting.
- Comments should be written in English.
- Update all affected languages when possible.

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

## GitHub File Retrieval

When asked to modify repository files:

- retrieve the current file first,
- verify that retrieval succeeded,
- avoid reconstructing files from memory.

Repository files are the source of truth.

---

## Editing and Verification Workflow

Before editing an existing source file, read the exact current file from the repository. Do not assume it matches
decompiled game patterns, older conversations, or nearby code.

For rules-maintenance tasks, edit only rule files such as `AGENTS.md` and files under `docs/`. Other repository files
may be changing in parallel by other agents; ignore unrelated non-rule changes instead of analyzing or cleaning them up.

For large or frequently edited files, prefer small sequential patches:

- dependency registration,
- fields and injected dependencies,
- method bodies,
- cleanup.

After editing, inspect the diff for unrelated changes, encoding-only changes, BOM changes, or line-ending noise. Run:

```powershell
git diff --check
```

Before committing, check:

```powershell
git status --short
```

Normal `git diff` output does not show untracked files, so use status to avoid missing new tests or accidentally
committing generated files.

Before submitting a change, run the tests relevant to the changed package.

Do not parallelize multiple `dotnet build` invocations for the same project and configuration unless they use isolated
intermediate output paths. Builds of the same project share `obj` and can fail with file-lock errors. Run compile-only
and package-copy builds sequentially.

For TimberDev-only changes, run:

```powershell
dotnet run --project TimberDev.Tests\TimberDev.Tests.csproj
```

Do not use other mods as a TimberDev validation gate by default. If a TimberDev change affects logic that another mod
actually uses, also run that mod's tests as downstream regression coverage. If the change touches a mod, run that mod's
own tests when they exist. If the change touches shared behavior used by multiple packages, run the affected package
tests as well.

For test-only changes, run the test project that was changed. Do not run downstream mod tests for test-only changes
unless the change also modifies shared production code or shared test infrastructure used by those downstream tests.

These relevant tests MUST pass before submitting the change.

Tests must not drive unapproved production-code changes. If adding or expanding tests reveals a production bug, dead
code, missing API, or design mismatch, stop and ask before changing production code unless the user explicitly asked to
fix it.

Configurator-only classes that only bind services or declare contexts do not need unit tests unless they contain
non-trivial logic.

When testing code that depends on Timberborn or game services, prefer the smallest useful test seam.

Test stubs are acceptable when the goal is to cut the game runtime out of unit tests, but keep them narrow. Model only
the behavior required by the tests being added, and do not expand stubs speculatively.

Unit tests that use local Timberborn stubs do not replace building the changed mod project. When a change touches game
extension methods, publicized game APIs, or code paths represented by test stubs, also run the changed mod project
build. Stubs may not model production namespace imports, extension-method resolution, publicized assembly shape, or
other compile-time details from the real game assemblies.

If a test requires large stubs, reflection into private construction, or duplicated lifecycle behavior, first try the
smallest test-only approach. If that becomes brittle or obscures the behavior under test, stop and ask whether a
minimal production-code testability change is acceptable.

When verifying a mod project, distinguish C# compilation failures from post-build target failures. Some projects copy
outputs to the local game mods folder after building, and that copy step may fail even when the DLL compiled.

If the user says not to run tests, interpret that as "do not run test suites" unless they also explicitly say not to
build. After code changes, still run the relevant compile or build check when possible.

For compilation-only verification of projects with mod-copy post-build targets, use a non-existent `ModPath`:

```powershell
dotnet build <ProjectPath>.csproj /p:ModPath=__NoSuchModPath__
```

Report which build command was used when the ordinary build has repository-specific side effects.

When a mod gameplay, runtime, or UI fix is ready for user real-game validation, a compile-only build is not enough.
Unless the user explicitly says not to write to `_MODS!`, run the normal mod build that updates the real local mod
output, then verify the expected DLL/XML or package artifact timestamp when practical. This prevents the user from
testing an old build in game.

---

## Branches

Inspect the repository before assuming branch structure.

Historically, release branches may exist for specific Timberborn versions.

Do not assume that main is always the only active branch.
