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
2. Identify which components are added.
3. Identify which services are registered.
4. Identify how the game object is assembled.

Many gameplay features become much easier to understand after locating the configurator.

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

Prefer extension over replacement.

Prefer existing architecture over custom architecture.

When unsure, spend more time exploring the game and less time writing code.