# Automation Mod Agent Notes

These notes apply only when working inside the Automation mod.

## Real-Game Validation Before Tests

For UI behavior that depends on the actual Timberborn interface, Harmony UI patches, or game-side interaction timing:

1. Implement the production change.
2. Build the mod into the real `_MODS!` folder.
3. Let the user verify the behavior in the game.
4. Add or update tests only after the in-game behavior is confirmed.

Do not spend time locking tests around an unverified UI approach.

When the user asks to validate gameplay or runtime Automation behavior in the real game before tests, pause test
implementation until confirmation. After confirmation, add focused tests for the behavior that was actually validated.

## Game Automation Conflicts

Automation may coexist with Timberborn's built-in game automation.

Do not treat every Advanced Automation rule as a conflict. A conflict exists only when an enabled rule has an action
that changes the building's gameplay state.

Rules that only set signals, log debug information, or show notifications should not block the game's built-in
automation controls.

When adding new scriptable actions, decide whether the action changes building state. If it does, update the game
automation conflict detector and its tests.

When showing conflicts in UI, distinguish blocking errors from informational warnings:

- If stock game automation is already enabled, active state-changing Advanced Automation rules cannot be saved while
  active.
- If stock game automation is not enabled, state-changing Advanced Automation rules may be saved, but the UI may warn
  that they will block stock game automation later.
- Disabled or deleted rules should not be treated as conflicts.

## Tooltips

Keep Automation tooltip text short and localized.

Use intentional line breaks in localization strings when a tooltip would otherwise become a long single-line panel.

## Signal Design

For player-visible signals with two named logical states, prefer a string signal with `ValueDef.Options` over a numeric
`0`/`1` signal. Automation's Rules UI can show string options as readable dropdown values.

For Automation signals that represent repeatable gameplay events, prefer modeling the event as state that changes on
every meaningful occurrence, such as a counter scoped to the current recipe or mode. Avoid generic same-value signal
notifications because they weaken the normal value-change semantics of signal updates. Use same-value notification only
if a stateful representation would be misleading or impossible.

When the Rules UI groups parameterized signals by meaning, use explicit `SignalDef` metadata such as
`DisplayNameLocKey` and its supported display-name argument. Do not infer semantic groups by splitting or comparing the
localized `DisplayName`; word, separator, and common-prefix heuristics are language-dependent and can invent misleading
subgroups.

Preserve the signal order supplied by each `ScriptableComponent` when creating groups. Do not globally sort the merged
signal list without an explicit product requirement. Place a group where its first member occurred and preserve the
members' relative order.

Keep display-name metadata aligned with the localization and UI contract that is actually implemented. The current
parameterized signal contract supports one display-name argument; do not introduce argument arrays or imply multiple
placeholder support until the complete localization and UI path supports it.

For global time-like signals, prefer game events, `ITimeTriggerFactory`, or similar scheduled triggers over
`AutomationService.RegisterTickable` polling. If exact per-tick precision is not required, use a coarse bucket and a
lazy one-shot trigger when there are listeners.

For Automation scriptable components that poll only while signals have subscribers, register the tick callback lazily
through `AutomationService.RegisterTickable` on the first listener and unregister it on the last listener. Use
`ITickableSingleton` only when the component must tick independently of script listeners.

For parameterized signal families backed by per-key polling caches, keep first-listener registration and last-listener
removal symmetric for every key. Remove a key from the polling cache after its last listener is removed.

Use one canonical value formula for a signal's direct read, initial tracker state, and tick update. Do not maintain a
partial approximation in the polling cache; it can suppress a real change or a required reset when district or owner
state changes.

In a demonstrated hot polling path, let calculation helpers return the raw representation consumed by cache comparison
and convert it to `ScriptValue` only at the scripting API boundary. Do not generalize this into a requirement for cold
paths where the extra representation would not remove measured wrapping and unwrapping.

Before adding a new Automation scriptable component with callbacks, ticking, trackers, or reference management, inspect
at least one nearby component with the same lifecycle shape and follow that lifecycle pattern unless there is a reason
not to.

Automation dynamic components derived from `AbstractDynamicComponent` and created through
`AutomationBehavior.GetOrCreate` are building-owned instances. Register these dynamic component types in DI as
`AsTransient()`, never `AsSingleton()`, so runtime state, saved state, callbacks, signals, and owner references cannot
leak between buildings.

Do not create persistent Automation dynamic components from template-owned `BaseComponent` or `TickableComponent`
`Awake()` methods when those dynamic components can also be restored from `AutomationBehavior.SavedComponents`.
`Awake()` runs before `IPersistentEntity.Load()` during world loading, so early `AutomationBehavior.GetOrCreate` calls
can create a component that load then tries to restore a second time. Prefer creating dynamic components from script or
action registration, or have the dynamic component attach itself to the template-owned component after the dynamic
component is created or restored.

When fixing load-time state mismatches, treat delayed execution mechanisms such as coroutines, end-of-frame callbacks,
parallel tick finalization, and scheduled updates as suspect until proven safe for the specific load phase. A delayed
callback can run later while still using incomplete load-stage game state. If a subsystem is not fully synchronized
during `OnEnterFinishedState` or similar callbacks, prefer an explicit post-load activation or synchronization hook
owned by the Automation behavior or service, and let dynamic components refresh cached state synchronously there.

When adding a new Automation signal family, decide whether each signal is building-scoped or global.

When adding an Automation `SignalDef`, always set `Scope` explicitly, even when it is
`SignalDef.ScopeEnum.Building`, so ownership semantics are visible at the definition site.

When a signal script-name segment is derived from an external game or mod identifier, such as a `GoodId`, recipe ID,
prefab ID, or template ID, do not insert the raw identifier directly. Use the shared script-safe segment codec/helper
and preserve raw or backward-compatible lookup behavior where existing scripts may depend on it.

For Automation helpers that define script-name serialization, encoding, or naming contracts, make the XML documentation
especially explicit. Document the exact format, which names remain raw, what gets encoded, decode failure behavior, and
backward-compatibility expectations for existing scripts.

When adding temporary compatibility migrations for legacy Automation scripts, rules, or saved actions, include a dated
removal comment and explain the legacy script/action name being supported. Review expired compatibility windows during
Automation maintenance or release preparation, and either remove the path or renew the date with justification.

Use `SignalDef.Scope` as the source of truth for exportability. Building signal export UI must rely on explicit scope,
not on script-name prefixes or deny-lists.

Global game, colony, district, weather, time, science, or service-state signals should use `SignalDef.ScopeEnum.Global`
unless a specific building actually owns and produces the value.

`District.ResourceCapacity` intentionally uses `ResourceCount.InputOutputCapacity`, not `TotalCapacity`. It represents
fillable storage that can accept and later provide the good; output-only buffers are excluded.

`District.ResourceFill` intentionally uses `clamp(AvailableStock / InputOutputCapacity, 0, 1)`, not
`ResourceCount.FillRate`. Preserve the edge cases `0 / 0 = 0` and positive stock with zero fillable capacity equal to
`1`.

When adding global signals, verify that the building signal export dialog does not list them.

When listing Automation inventory signals, do not treat `Inventory.GetCapacity(...)` as the only source of signal
availability. The rules UI and building-signal export should list script signals that are actually supported even when
current capacity enumeration is empty. For manufactories, also check `Manufactory.CurrentRecipe` and the inventory's
input or output goods. For stockpiles, also check the `SingleGoodAllower` assigned good; Empty mode can hide current
capacity while the assigned storage good remains valid.

When exposing a network, district, graph, or other aggregate as a building signal, keep the signal building-scoped if
users select a building, but read the current game-owned aggregate object at evaluation or tick time. Do not cache the
aggregate owner as the source of truth unless the game API provides stable lifecycle events that keep the cache
correct.

## Standalone Parser Harness

`TestParser` is a supported downstream parser harness, not an active mod package and not a substitute for
`Automation.Tests`. When an Automation change affects parser behavior, the expression model, `ExpressionDescriber`,
`ValueDef` contracts, or source files linked by `TestParser`, keep its linked compile includes and local stubs
synchronized, then run:

```powershell
dotnet run --project TestParser/TestParser.csproj
```

The harness must return a nonzero exit code when any expected-pass sample, expected-fail sample, round trip, execution,
description, or decompile check fails. Do not accept a successful process exit that merely prints failed samples.
