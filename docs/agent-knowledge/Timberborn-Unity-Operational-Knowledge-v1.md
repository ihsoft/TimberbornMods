# Timberborn Unity Operational Knowledge

## Purpose

Explain how Timberborn Unity source, imported references, exported packages, C# outputs, and release artifacts relate.
Use this model to identify ownership and select the correct pipeline without treating generated output as authoring
source.

This document does not repeat environment setup, package-specific commands, UI patterns, diagnostics, or release
procedure. Read bootstrap, Repository Validation Operational Knowledge, UI Toolkit notes, Diagnostics Operational
Knowledge, and release rules when their routing conditions apply.

## Artifact Model

| Layer | Typical location | Owner and state |
|---|---|---|
| Unity authoring source | `ModsUnityProject/Assets/Mods/<ModName>` | repository authors; tracked unless specifically documented otherwise |
| Modding Tools source | `ModsUnityProject/Assets/Tools` | tracked tool integration already present in this repository |
| Imported game references | `Assets/Plugins/Timberborn` and `Assets/Tools/ImportedAssets` | official importer; local generated game-version-specific dependencies |
| Exported local package | `_MODS!/<ModName>` and its compatibility lanes | Unity exporter and C# build; generated real-game input |
| Script output | package `Scripts/<Assembly>.dll` and optional XML | C# project build; generated package content |
| Release artifact | configured package source, staging output, or ZIP | release tooling; exact input or output for a release operation |

Do not edit imported references, exported packages, or release staging as substitutes for changing tracked authoring
source. Generated output may be inspected and validated, but a later import, export, build, or release can overwrite it.

## Source Of Truth By Operation

- Implementation uses tracked C# and Unity authoring files as source.
- Unity export uses the mod's tracked Unity source and selected compatibility lane as input; `_MODS!` is output.
- Release packaging uses only the exact configured `Package.SourcePath` as package-content input.

If the configured release source is stale or incomplete, refresh it through the normal export and build pipelines. Do
not reconstruct it during release from tracked source, another folder, or an older ZIP. Authoring ownership and release
input are different contexts, not competing definitions of one source of truth.

## Select The Pipeline

Use the changed files and the package matrix:

| Change | Required package refresh |
|---|---|
| C# only | build the mod project into the resolved real `ModPath` |
| Unity-owned package data only | export the selected compatibility lane through repository Unity tooling |
| C# and Unity-owned data | export Unity data first, then build C# into that exported package |
| imported DLLs or assets are missing or stale | run the official interactive importer workflow from bootstrap |

A successful C# build does not prove that Unity-owned data, localization, assets, or bundles were exported. A Unity
export does not prove a separately built DLL is current unless the invoked export command explicitly builds code.

For split ownership, such as a project-local `Mod` directory plus Unity resources, inspect every changed path and run
each owning pipeline. The package matrix records known split layouts.

## Imported References And Compatibility Lanes

A clean checkout is not Unity-ready until the official importer has produced the required game DLLs. Follow bootstrap
for first open, licensing, exact Editor version, and importer readiness.

`Assets/Tools/ImportedAssets` is ignored, local, and version-specific. Never force-add it. Imported DLLs and assets are
evidence for the game version that produced them; verify provenance before relying on their API or data shape.

`version-X.Y` directories are compatibility lanes, not folders required for every game patch. Select or add a lane only
from the package matrix, a documented local exception, release configuration, or an explicit compatibility need.

Unity manifest version, C# assembly version, release metadata, and compatibility lane serve different consumers. Do
not assume changing one automatically updates the others.

## Post-Import Assembly Gate

After changing the Timberborn game version, Unity Editor version, or Unity package versions and refreshing imported
game assemblies, do not accept copied-file summaries, importer completion, or process exit code as sufficient proof of
a valid import.

Before asset export, inspect the current Unity or importer log for every `Assembly ... will not be loaded due to errors`
message and other assembly-load failures. Classify each occurrence. Any unresolved failure involving an imported game
assembly, a Unity assembly it requires, or their dependency cascade is a hard stop for export even if Unity can still
build an asset bundle.

Trace each cascade from its first missing or incompatible dependency. Later Timberborn assemblies failing to load may
only be consequences of that earlier dependency failure; do not fix or dismiss them independently.

When Unity or package versions change, inspect both `Packages/manifest.json` and `Packages/packages-lock.json` for
dependencies that changed depth, disappeared, or were previously available only transitively. Do not assume a package
required by imported game assemblies will remain transitively resolved after an Editor or render-pipeline upgrade.

If imported game assemblies reference `UnityEngine.UI`, verify that `com.unity.ugui` is explicitly resolved for the
current Editor and that the required Unity UI assembly is available before accepting the import. Apply the same
dependency reasoning to other missing Unity assemblies rather than hard-coding one historical package version.

For UI-dependent exports, also verify that the relevant Timberborn UI assemblies load and their custom UXML element
types are available in the Editor. Absence of compile errors in the mod's own assets does not prove that imported custom
controls were registered.

## Export Boundary

Before export, resolve the package owner and lane, satisfy bootstrap readiness, close an interactive Editor using the
same project, and inspect the current `tools/export-unity-mod.ps1` parameters. Use repository export tooling rather than
hand-copying source into `_MODS!`; copying can miss filtering, platform bundles, layout, or lanes.

After export, verify the intended local output: lane, manifest identity/version, changed data or bundles, and current
timestamps or content. When code is involved, also verify the DLL/XML produced by the C# pipeline.

If export was not run or output cannot be verified, report that the local package was not refreshed. Route failures to
Diagnostics Operational Knowledge; route first-open or licensing failures to bootstrap.

## Handoff

The refreshed local package is an input to user real-game validation, not proof of correct behavior. Follow the root
real-game gate before regression tests or commit.

For release, follow release-specific ordering and verify that the exact configured package source contains the exported
assets and built scripts intended for publication.
