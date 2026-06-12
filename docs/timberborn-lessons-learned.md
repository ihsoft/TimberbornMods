# Timberborn Lessons Learned

## Purpose

This document contains practical discoveries, pitfalls, and architectural lessons learned while developing Timberborn mods.

Unlike formal rules, these notes represent experience gathered from real projects.

---

## Initialization Order

Do not assume that ILoadableSingleton.Load() runs before all relevant game systems.

Some Harmony patches and UI code may execute earlier.

When providing services to static code:

- constructor initialization may be required,
- verify execution order.

---

## Publicizer First

Before using reflection:

1. Check whether assemblies are publicized.
2. Prefer direct access whenever possible.

Reflection should be the exception, not the default approach.

---

## AccessTools Usage

If reflection is required:

- cache accessors,
- avoid repeated AccessTools lookups,
- avoid reflection inside frequently executed code.

---

## Harmony Scope

Prefer small targeted patches.

Large patches increase maintenance cost and game update risk.

---

## Save/Load Validation

Every new persistent feature should be tested through:

1. New game.
2. Save.
3. Load.
4. Continue playing.

Many bugs only appear after loading.

---

## UI Integration

Prefer extending existing panels instead of replacing them.

Smaller UI modifications are usually more resilient to game updates.

---

## Performance Awareness

Always determine execution frequency before optimizing.

The difference between:

- startup,
- panel open,
- game tick,
- frame update,
- per-building update

is often more important than the code itself.

---

## Research Before Coding

The fastest implementation is often achieved by spending more time studying existing game systems.

Find the closest existing implementation first.

Copy architecture, not implementation.

---

## Evidence Over Assumptions

Do not invent:

- APIs,
- services,
- extension points,
- configuration formats.

Verify assumptions against actual code and actual game data.