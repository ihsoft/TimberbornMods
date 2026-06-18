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

## State Transition Timing

When a feature depends on an exact state-transition edge, identify the game method that owns the transition.

Polling a calculated state after the fact can miss the semantic edge if the game resets or restarts the state inside
the same lifecycle method.

When the mod needs the final state after game logic has reacted to an event, patch after the owning game method instead
of subscribing to a lower-level event that fires earlier.

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

## Game UI Verification Before Tests

For changes that depend on in-game UI behavior, Harmony UI patches, or visually sensitive interactions:

1. Implement the production change.
2. Build it into the real mod folder.
3. Let the user verify it in the actual game.
4. Add or adjust tests after the real-game behavior is confirmed.

Tests written before a manual game check can lock in the wrong approach and waste time.

---

## Tooltip Text

Tooltip text must be short, localized, and sized for the actual Timberborn UI.

Long tooltip lines can run across the screen or look broken even when technically visible.

When a tooltip needs more than a few words, add an intentional line break in the localization string to keep the
tooltip compact.

---

## Incremental Design Work

When the user says the broader task is not finished, treat each commit as a confirmed working step, not as the end of
the design.

Keep the unresolved context in mind for follow-up changes and avoid restarting the topic from scratch.

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

---

## PowerShell WhatIf

Do not assume a repository script supports `-WhatIf` unless it declares `CmdletBinding(SupportsShouldProcess)`.

If a script is expected to be used in dry-run checks, implement real `ShouldProcess` handling around filesystem writes.
