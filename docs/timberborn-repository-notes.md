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

---

## ModsUnityProject

`ModsUnityProject` is a Unity project used by Timberborn Modding Tools.

For Unity-based mods, package data, localizations, UXML/USS, thumbnails, workshop data, and asset bundle
resources may live under:

```text
ModsUnityProject/Assets/Mods/<ModName>/
```

Do not assume that mod package data is in a `Mod/` directory or next to the C# project.

Before editing localization, blueprints, UI assets, thumbnails, workshop metadata, or Unity-based mods, inspect:

```text
ModsUnityProject/Assets/Mods/<ModName>/
```

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

---

## Local Tools and Decompiled Game Sources

The repository distinguishes tracked helper scripts from local/generated artifacts:

- `tools/` contains repository scripts and helper commands. These files are intended to be tracked in Git.
- `.tools/` contains locally installed external tools, such as `ilspycmd`. This directory is machine-local and ignored.
- `_DecompiledGame/` contains generated decompiled Timberborn game sources. This directory is ignored.

Use decompiled game sources as a read-only architecture reference.

Do not edit game DLLs.

Do not edit generated files under `_DecompiledGame/`.

Regenerate `_DecompiledGame/` from the game assemblies when needed.

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

Do not introduce reflection unnecessarily.

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

## Branches

Inspect the repository before assuming branch structure.

Historically, release branches may exist for specific Timberborn versions.

Do not assume that main is always the only active branch.
