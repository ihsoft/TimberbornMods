# Timberborn Domain Knowledge

## Purpose

Provide a compact baseline of stable Timberborn mod-development concepts.

This document defines domain knowledge only. Engineering judgment belongs to Engineering Professional. Communication
style belongs to Ada Personality. Repository workflow, paths, ownership, and publication procedure belong to the
current repository instructions.

## Game and mod architecture

Understand how these concepts relate:

- Timberborn game systems and player-facing behavior;
- mod structure, packaging, loading, and compatibility;
- BepInEx integration;
- configurators, dependency injection, and service lifecycle;
- Harmony patching and execution timing;
- public and publicized game assemblies;
- Unity resources and assets;
- localization;
- shared mod libraries and ownership boundaries.

## Dependency injection and lifecycle

Construction, `ILoadableSingleton.Load()`, UI creation, and Harmony patch execution do not have one universal order.
UI-related or other early patches may run after a singleton is constructed but before `Load()`.

When static patch code needs a service from dependency injection, verify the actual execution order and initialize the
bridge at the earliest safe lifecycle point. Read the task-specific modding rules for the established implementation
pattern instead of inventing a new bridge.

## Publicized assemblies

Before assuming reflection or Harmony `AccessTools` is necessary, inspect the project for
`BepInEx.AssemblyPublicizer.MSBuild` and references with `Publicize="true"`. When the required assembly is publicized,
direct access to private or internal members may be available and is an accepted Timberborn modding technique.

## Harmony

Understand patch target selection, prefix/postfix/transpiler roles, execution timing, static-state risks, and interaction
with dependency injection. Harmony is an integration mechanism, not the default architecture; use existing extension
points when they fit.

## Unity resources, localization, and packaging

Understand that code assemblies, Unity resources, localization data, package metadata, and mod loading are related but
may have different source and build paths. Determine the actual project layout and export pipeline before changing or
packaging them.

## Knowledge boundaries

Read detailed implementation patterns from the task-specific modding rules. Read repository-specific paths,
conventions, package ownership, shared-library roles, and validation commands from the current repository notes and
local instruction files. Treat lessons learned as evidence-backed pitfalls, not as a replacement for current rules.
