# Timberborn Repository Notes

## Purpose

This document contains repository-specific knowledge that is useful when working with the TimberbornMods repository.

Unlike the general modding guides, the contents of this document are specific to this repository and its development practices.

---

## Repository Structure

This repository contains multiple Timberborn mods and shared libraries.

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

## Shared Libraries

### TimberCommons

Player-facing shared functionality.

Contains reusable systems that may be used by multiple mods.

When modifying TimberCommons, consider potential impact on all dependent mods.

### TimberDev

Developer-facing shared functionality.

Contains utilities intended to support mod development.

Shared functionality that is not player-facing may belong here instead of TimberCommons.

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