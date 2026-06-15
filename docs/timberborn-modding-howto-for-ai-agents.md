# Timberborn Modding How-To for AI Agents

## Purpose

This document describes how to approach creation of a new Timberborn mod.

It focuses on discovering existing game systems, understanding game architecture, and integrating with Timberborn conventions.

It is not a coding style guide.

It is not a repository-specific guide.

The goal is to help AI agents create mods that feel like a natural part of the game rather than isolated additions.

---

## General mindset

Before creating a new system, determine whether a similar system already exists in the game.

Timberborn already contains solutions for many common gameplay problems.

Prefer extending existing systems over replacing them.

When implementing a new feature:

1. Identify a similar game feature.
2. Study how it is implemented.
3. Reuse the same architectural approach whenever possible.

Copy architecture, not implementation.

---

## Evidence-Based Development

Do not assume game behavior.

Do not assume API capabilities.

Do not invent classes, services, methods, extension points, configuration files, or game systems.

Whenever possible:

1. Inspect actual game code.
2. Inspect actual game data.
3. Inspect actual repository code.
4. Verify assumptions before building on them.

If required information is unavailable, explicitly state the limitation instead of guessing.

Incorrect certainty is usually more harmful than incomplete information.

---

## Understand the game before writing code

Do not start by writing Harmony patches.

Do not start by creating new services.

Do not start by creating new data structures.

Do not start by creating custom save systems.

First understand how the game currently solves the problem.

The usual order is:

1. Find relevant game content.
2. Find the classes behind it.
3. Find dependency registration.
4. Find UI integration points.
5. Find save/load behavior.
6. Only then start implementing changes.

Time spent understanding the existing implementation is usually repaid many times during development.

---

## Retrieve sources before modifying them

When modifying an existing file:

1. Read the current version first.
2. Verify that the retrieved content is complete.
3. Preserve existing content unless explicitly instructed otherwise.
4. Make the smallest change that satisfies the request.

Never reconstruct existing files from memory.

Never assume file contents based on previous conversations.

If the current file cannot be retrieved reliably, explicitly state the problem instead of generating a partial reconstruction.

---

## Discover existing game systems

When adding a new gameplay feature, first identify the closest existing feature.

Examples:

| Desired feature | Existing system to study |
| --- | --- |
| New building behavior | Similar building |
| New workplace behavior | Existing workplaces |
| New production logic | Existing production buildings |
| New tool | Existing tools |
| New panel | Existing panels |
| New overlay | Existing overlays |
| New status | Existing statuses |
| New notification | Existing notifications |
| New resource logic | Existing resource systems |

The closest existing implementation is usually the best starting point.

---

## Start from game data

Gameplay behavior is often easier to discover through game data than through code.

Before searching for classes:

- inspect building definitions,
- inspect templates,
- inspect blueprint data,
- inspect configuration files,
- inspect localization identifiers.

When `_ExtractedGameAssets/` is available, use it as a read-only game data reference:

- inspect `_ExtractedGameAssets/Blueprints/` for blueprint and component-spec patterns,
- inspect `_ExtractedGameAssets/Localizations/` for existing game localization keys and terminology,
- inspect `_ExtractedGameAssets/UI/` for UXML, USS, sprites, stable element names, and UI hierarchy,
- inspect `_ExtractedGameAssets/Shaders/` only for shader-specific work.

If `_ExtractedGameAssets/` is missing and game data or UI structure is needed, regenerate it from the archives under:

```text
_GAME!/Timberborn_Data/StreamingAssets/Modding/
```

using:

```powershell
tools/extract-game-modding-assets.ps1
```

Determine:

- which components are used,
- which systems participate,
- which UI elements are connected,
- which services appear to own the behavior.

Then trace those components back to code.

Game data often reveals the architecture much faster than searching source code blindly.

---

## Follow the ownership chain

When investigating a gameplay feature, identify:

- who owns the data,
- who updates the data,
- who consumes the data,
- who displays the data.

Many bugs come from modifying the wrong layer of the system.

Always understand the ownership chain before introducing changes.

---

## Learn the dependency graph

Timberborn relies heavily on dependency injection.

When studying a system:

1. Find the main class.
2. Find where it is registered.
3. Identify its dependencies.
4. Identify who consumes it.

Understanding the dependency graph is often more important than understanding individual methods.

A service with many consumers is usually more important than a helper method with complex logic.

---

## Prefer dependency injection

When a game service already exists, obtain it through dependency injection.

Prefer:

```csharp
class Example {
  readonly ToolManager _toolManager;

  public Example(ToolManager toolManager) {
    _toolManager = toolManager;
  }
}
```

Avoid introducing global access patterns unless the game architecture already uses them.

Follow the architecture of the surrounding code.

---

## Understand configurators

Configurators are often the entry point for game systems.

When studying a feature:

1. Find the configurator.
2. Identify which context it runs in.
3. Identify which services are registered.
4. Identify which components are registered.
5. Identify whether bindings are singleton or transient.
6. Identify whether any components are added through `TemplateModule` decorators.
7. Identify how the game object is assembled.

Many gameplay features become much easier to understand after locating the configurator.

When creating a new package, create a package configurator and register every DI-participating type defined by that
package.

Use the smallest correct context:

- `Game`,
- `MainMenu`,
- `MapEditor`,
- rarely `Bootstrapper`.

If an entity component is attached from a blueprint/spec, check whether a `TemplateModule.Builder().AddDecorator(...)`
registration is required.

---

## Understand specifications and models

Many Timberborn systems separate:

- configuration,
- runtime state,
- presentation.

Classes such as:

- `Spec`,
- `Model`,
- `Factory`,
- `Service`,

often represent different responsibilities.

Avoid merging responsibilities when creating new code.

Follow the existing separation whenever possible.

---

## Treat Harmony as a last resort

Harmony is powerful but increases maintenance cost.

Before creating a patch, check whether the problem can be solved by:

- dependency injection,
- component registration,
- service registration,
- configurators,
- UI extension,
- existing extension points.

Use Harmony only when no reasonable extension point exists.

---

## Keep patches minimal

When Harmony is required:

- patch the smallest possible target,
- prefer Postfix over Prefix when possible,
- avoid replacing entire methods,
- avoid duplicating original logic,
- avoid large transpilers unless absolutely necessary.

The smaller the patch, the more resilient it is to game updates.

---

## Minimal Change Principle

When modifying existing code:

- preserve architecture,
- preserve behavior,
- preserve formulas,
- preserve thresholds,
- preserve logging,
- preserve comments,
- preserve formatting conventions.

Change only what is required by the task.

Avoid opportunistic refactoring unless explicitly requested.

A correct minimal change is usually preferable to a larger "improved" solution.

---

## Reuse existing data

Avoid creating duplicate state.

If the game already stores information, use the existing source whenever possible.

Prefer extending existing systems over mirroring them.

Duplicated state often creates:

- save/load problems,
- synchronization bugs,
- migration issues,
- debugging difficulties.

Use a single source of truth whenever possible.

---

## Think about save/load early

Every new piece of state should answer:

> What happens after saving and loading the game?

Do not postpone persistence design.

Verify:

- save behavior,
- load behavior,
- migration behavior,
- default values,
- compatibility with existing saves.

When a UI action creates persistent gameplay state or a new state source, define its full lifecycle before implementing:
creation, save/load, update, and removal. If the UI can create or attach the state, make sure there is an explicit and
understandable way to remove the same state, and that the UI wording matches the actual effect.

A feature that works in a fresh game but breaks after loading is not finished.

---

## Understand update frequency

Before adding logic, determine how often it executes.

Examples:

- once during startup,
- once during registration,
- once when opening a panel,
- once per game tick,
- once per rendered frame,
- once per building,
- once per district.

Frequency strongly affects performance requirements.

Always ask:

> How often will this code run?

before optimizing or introducing additional complexity.

---

## Extend UI instead of replacing it

Prefer:

- adding controls,
- adding sections,
- adding tabs,
- adding overlays,
- extending existing panels.

Avoid replacing entire windows unless absolutely necessary.

Users benefit from consistency with existing game UI.

A small extension is usually easier to maintain than a complete replacement.

---

## Localize everything

User-facing strings should be localizable.

Do not hardcode visible text.

Assume every player may use a different language.

All user-visible content should be prepared for localization from the beginning.

Adding localization later is usually more expensive.

---

## Preserve player expectations

Players already understand how Timberborn behaves.

Whenever possible:

- use existing terminology,
- use existing UI conventions,
- use existing interaction patterns,
- use existing visual language.

A feature that behaves similarly to existing game systems is easier for players to learn.

---

## Learn common architectural patterns

When exploring game code, pay special attention to classes named:

- Configurator
- Spec
- Factory
- Service
- Model
- Tool
- Panel
- Fragment
- Tab

These often represent important extension points.

Understanding these classes frequently reveals the overall architecture of the feature.

---

## Study before extending

Before creating a new implementation:

- find the existing implementation,
- verify assumptions against actual code and data,
- understand the data flow,
- understand the dependency graph,
- understand persistence,
- understand UI integration.

Only then start modifying or extending the system.

Understanding the architecture usually reduces the amount of code that needs to be written.

---

## Typical workflow for creating a new mod

1. Define the gameplay goal.
2. Find the closest existing feature.
3. Study game data.
4. Find related classes.
5. Understand ownership of the data.
6. Understand dependencies.
7. Find extension points.
8. Avoid Harmony if possible.
9. Implement gameplay logic.
10. Implement UI.
11. Add localization.
12. Verify save/load behavior.
13. Test performance implications.
14. Test compatibility with existing saves.

---

## Final principle

The best Timberborn mods feel like a natural part of the game.

Prefer solutions that integrate with existing systems.

Prefer understanding over patching.

Prefer evidence over assumptions.

Prefer extension over replacement.

Prefer existing architecture over custom architecture.

When unsure, spend more time exploring the game and less time writing code.
