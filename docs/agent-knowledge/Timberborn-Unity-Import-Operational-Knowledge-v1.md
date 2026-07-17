# Timberborn Unity Import Operational Knowledge

## Purpose

Provide the validation workflow for Timberborn game-assembly imports after a clean first open or a change to the game,
Unity Editor, render pipeline, or Unity package graph.

This document does not replace:

- bootstrap rules for installing Unity, licensing, Hub project arguments, and the official first-open importer steps;
- Unity Operational Knowledge for artifact ownership, compatibility lanes, and export;
- Diagnostics Operational Knowledge for bounded investigation of a failed import;
- release rules for package preparation or publication.

## Imported Reference Ownership

A clean checkout is not Unity-ready until the official importer has produced the required game DLLs and assets. Follow
bootstrap for first open, licensing, the exact Editor version, and the official interactive import workflow.

`Assets/Plugins/Timberborn` and `Assets/Tools/ImportedAssets` are local, generated, game-version-specific dependencies.
Do not edit or track them as authoring source. `ImportedAssets` must remain ignored and must never be force-added.

Imported DLLs and assets are evidence only for the game version and Unity environment that produced them. Verify their
provenance before relying on their API, data shape, or custom UI types.

## Refresh Sequence

When the game, Editor, render pipeline, or Unity package graph changes:

1. Record the target game version and the Editor version from `ProjectSettings/ProjectVersion.txt`.
2. Inspect the intended changes to `Packages/manifest.json` and the resolved changes in `Packages/packages-lock.json`.
3. Satisfy bootstrap licensing and first-open prerequisites, acquire `unity-project` through Repository Coordination
   Operational Knowledge, then run the official importer against the verified game installation.
4. Wait for Unity import and compilation to finish; do not treat copied-file completion as the end of validation.
5. Apply the package-dependency and assembly-load gates below.
6. Hand off to Unity export only after imported game assemblies and required custom types resolve cleanly.

## Package Dependency Drift

When Unity or package versions change, inspect both `Packages/manifest.json` and `Packages/packages-lock.json` for
dependencies that changed depth, disappeared, or were previously available only transitively. Do not assume a package
required by imported game assemblies will remain transitively resolved after an Editor or render-pipeline upgrade.

If imported game assemblies reference `UnityEngine.UI`, verify that `com.unity.ugui` is explicitly resolved for the
current Editor and that the required Unity UI assembly is available before accepting the import. Apply the same
dependency reasoning to other missing Unity assemblies rather than hard-coding one historical package version.

## Post-Import Assembly Gate

Do not accept importer summaries, copied-file success, compilation of the mod's own assets, or process exit code as
sufficient proof of a valid game-assembly import.

Inspect the current Unity or importer log for every `Assembly ... will not be loaded due to errors` message and other
assembly-load failures. Classify each occurrence. Any unresolved failure involving an imported game assembly, a Unity
assembly it requires, or their dependency cascade is a hard stop for export even if Unity can still build an asset
bundle.

Trace each cascade from its first missing or incompatible dependency. Later Timberborn assemblies failing to load may
only be consequences of that earlier dependency failure; do not fix or dismiss them independently.

## Custom UI Type Gate

For UI-dependent exports, verify that the relevant Timberborn UI assemblies load and their custom UXML element types are
available in the Editor. Absence of compile errors in the mod's own UXML or assets does not prove that controls such as
Timberborn `NineSlice` elements were registered.

Do not export UI-dependent bundles while imported UI assemblies or required custom element types are unresolved. A
bundle can be created successfully and still fail to instantiate those controls in the game.

## Handoff

After a clean import, continue with Unity Operational Knowledge to select the compatibility lane and export pipeline.
Route missing tools, licensing, or first-open prerequisites to bootstrap. Route an unresolved import or dependency
cascade to Diagnostics Operational Knowledge using the first failing dependency as the starting evidence.
