# Timberborn Unity Operational Knowledge

## Purpose

Explain how Timberborn Unity source, imported references, exported packages, C# outputs, and release artifacts relate.
Use this model to identify ownership and select the correct pipeline without treating generated output as authoring
source.

This document does not repeat environment setup, importer validation, package-specific commands, UI patterns,
diagnostics, or release procedure. Read Unity Import Operational Knowledge, bootstrap, Repository Validation Operational
Knowledge, UI Toolkit notes, Diagnostics Operational Knowledge, and release rules when their routing conditions apply.

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
| imported DLLs or assets are missing or stale | follow Unity Import Operational Knowledge and the official interactive importer workflow from bootstrap |

A successful C# build does not prove that Unity-owned data, localization, assets, or bundles were exported. A Unity
export does not prove a separately built DLL is current unless the invoked export command explicitly builds code.

For split ownership, such as a project-local `Mod` directory plus Unity resources, inspect every changed path and run
each owning pipeline. The package matrix records known split layouts.

## Compatibility Lanes

`version-X.Y` directories are compatibility lanes, not folders required for every game patch. Select or add a lane only
from the package matrix, a documented local exception, release configuration, or an explicit compatibility need.

Unity manifest version, C# assembly version, release metadata, and compatibility lane serve different consumers. Do
not assume changing one automatically updates the others.

## Export Boundary

Before export, resolve the package owner and lane, satisfy applicable bootstrap and import readiness, and inspect the
current `tools/export-unity-mod.ps1` parameters. Follow Repository Coordination Operational Knowledge and acquire the
main repository's shared-machine Unity lock before checking for or launching Unity, even when the work uses a separate
project. The export wrapper does this automatically, then checks for an interactive Editor using the same project. Use
repository export tooling rather than hand-copying source into `_MODS!`; copying can miss filtering, platform bundles,
layout, or lanes.

After export, verify the intended local output: lane, manifest identity/version, changed data or bundles, and current
timestamps or content. When code is involved, also verify the DLL/XML produced by the C# pipeline.

If export was not run or output cannot be verified, report that the local package was not refreshed. Route failures to
Diagnostics Operational Knowledge; route first-open or licensing failures to bootstrap.

## Handoff

The refreshed local package is an input to user real-game validation, not proof of correct behavior. Follow the root
real-game gate before regression tests or commit.

For release, follow release-specific ordering and verify that the exact configured package source contains the exported
assets and built scripts intended for publication.
