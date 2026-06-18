# Automation Mod Agent Notes

These notes apply only when working inside the Automation mod.

## Real-Game UI Workflow

For UI behavior that depends on the actual Timberborn interface, Harmony UI patches, or game-side interaction timing:

1. Implement the production change.
2. Build the mod into the real `_MODS!` folder.
3. Let the user verify the behavior in the game.
4. Add or update tests only after the in-game behavior is confirmed.

Do not spend time locking tests around an unverified UI approach.

## Game Automation Conflicts

Automation may coexist with Timberborn's built-in game automation.

Do not treat every Advanced Automation rule as a conflict. A conflict exists only when an enabled rule has an action
that changes the building's gameplay state.

Rules that only set signals, log debug information, or show notifications should not block the game's built-in
automation controls.

When adding new scriptable actions, decide whether the action changes building state. If it does, update the game
automation conflict detector and its tests.

## Tooltips

Keep Automation tooltip text short and localized.

Use intentional line breaks in localization strings when a tooltip would otherwise become a long single-line panel.
