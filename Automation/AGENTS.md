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

For global time-like signals, prefer game events, `ITimeTriggerFactory`, or similar scheduled triggers over
`AutomationService.RegisterTickable` polling. If exact per-tick precision is not required, use a coarse bucket and a
lazy one-shot trigger when there are listeners.

For Automation scriptable components that poll only while signals have subscribers, register the tick callback lazily
through `AutomationService.RegisterTickable` on the first listener and unregister it on the last listener. Use
`ITickableSingleton` only when the component must tick independently of script listeners.

Before adding a new Automation scriptable component with callbacks, ticking, trackers, or reference management, inspect
at least one nearby component with the same lifecycle shape and follow that lifecycle pattern unless there is a reason
not to.

When adding a new Automation signal family, decide whether each signal is building-scoped or global.

When adding an Automation `SignalDef`, always set `Scope` explicitly, even when it is
`SignalDef.ScopeEnum.Building`, so ownership semantics are visible at the definition site.

Use `SignalDef.Scope` as the source of truth for exportability. Building signal export UI must rely on explicit scope,
not on script-name prefixes or deny-lists.

Global game, colony, district, weather, time, science, or service-state signals should use `SignalDef.ScopeEnum.Global`
unless a specific building actually owns and produces the value.

When adding global signals, verify that the building signal export dialog does not list them.

When exposing a network, district, graph, or other aggregate as a building signal, keep the signal building-scoped if
users select a building, but read the current game-owned aggregate object at evaluation or tick time. Do not cache the
aggregate owner as the source of truth unless the game API provides stable lifecycle events that keep the cache
correct.
