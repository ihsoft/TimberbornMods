# TimberCommons Modding Notes for AI Agents

This document contains notes specific to the TimberCommons mod.

Use this together with:

- repository-level `csharp-formatting-rules-for-ai-agents.md`
- repository-level `timberborn-modding-rules-for-ai-agents.md`

## Project role

TimberCommons is a regular player-facing Timberborn mod.

It is named "Commons" because it contains many small gameplay and UI changes, not because it is a general shared
library.

The important reusable API surface is the irrigation tower system. Components based on `IrrigationTower`, such as
`GoodConsumingIrrigationTower` and `ManufactoryIrrigationTower`, may be used by other mods, including third-party
mods.

When changing the irrigation tower components, specs, save/load behavior, or public API, consider compatibility for
external mods. Do not treat unrelated TimberCommons features as reusable infrastructure unless there is evidence.

When changing `IrrigationTower`, `GoodConsumingIrrigationTower`, `ManufactoryIrrigationTower`, `IRangeEffect`, or
their specs/effects, check whether the Timber Commons Wiki needs to be updated:

```text
https://github.com/ihsoft/TimberbornMods/wiki/Timber-Commons
```

## Data location

TimberCommons is a Unity-based mod.

Its package data is located here:

```text
ModsUnityProject/Assets/Mods/TimberCommons/Data/
```

Localization files are located here:

```text
ModsUnityProject/Assets/Mods/TimberCommons/Data/Localizations/
```

Known localization files:

```text
enUS.txt
ruRU.txt
deDE.txt
frFR.txt
```

When adding user-facing TimberCommons text, update all existing localization files when practical.

## Localization key prefix

For Common UI patches, prefer keys under:

```text
IgorZ.TimberCommons.CommonUIPatches
```

Example:

```csharp
const string ShowActiveModsLocKey = "IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods";
const string ShowAllModsLocKey = "IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods";
```

## Mod manager active-mod filter

TimberCommons currently patches the Timberborn mod manager list via:

```text
TimberCommons/CommonUIPatches/ModListViewInitializePatch.cs
```

The feature adds a button near the stock `ResetOrderButton`.

Behavior:

- When all mods are visible, button text should be `Show active ({activeCount}/{totalCount})`.
- When only active mods are visible, button text should be `Show all ({totalCount})`.
- The filter hides/shows existing `ModItem.Root` UI elements.
- It should not rebuild the mod list.
- It should not patch `OnModToggled`.
- If the user disables a mod while filtered, the row does not need to disappear immediately.

Relevant game classes observed via ILSpy:

```text
Timberborn.ModdingUI.ModListView
Timberborn.ModdingUI.ModItem
Timberborn.ModManagerSceneUI.ModManagerScenePanel
```

Useful game APIs:

```csharp
ModPlayerPrefsHelper.IsModEnabled(mod)
item.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
```

Private field access used by the patch:

```csharp
static Dictionary<Mod, ModItem> GetModItems(ModListView modListView) {
  return AccessTools.FieldRefAccess<ModListView, Dictionary<Mod, ModItem>>(modListView, "_modItems");
}
```

## Mod manager scene caveat

The Timberborn mod manager dialog can appear at an early startup stage before any mods are loaded.

At that stage, TimberCommons patches are not active yet.

Do not try to make the TimberCommons button appear in the pre-mod-load version of the mod manager unless explicitly requested.
It is acceptable that the button appears only after TimberCommons itself is loaded.

## `ILoc` bridge for `ModListViewInitializePatch`

`ModListViewInitializePatch` needs localized button text.

The patch is static, so `ILoc` is passed through a static bridge:

```csharp
static ILoc _loc = null!;

public static void SetLoc(ILoc loc) {
  _loc = loc;
}
```

Important observed lifecycle detail:

`ModListViewInitializePatch` can run before `ILoadableSingleton.Load()`, but after DI constructs the singleton.

Therefore, initialize the patch from the singleton constructor:

```csharp
sealed class ModListViewLocInitializer : ILoadableSingleton {
  public ModListViewLocInitializer(ILoc loc) {
    ModListViewInitializePatch.SetLoc(loc);
  }

  public void Load() {
  }
}
```

Do not move `SetLoc(loc)` into `Load()` for this patch unless the lifecycle is re-tested.

## Localization strings for the active-mod filter

Use these IDs:

```text
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods
```

English:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Show active ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Show all ({0})","Button in mod manager. Shows all installed mods."
```

Russian:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Показать активные ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Показать все ({0})","Button in mod manager. Shows all installed mods."
```

German:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Aktive anzeigen ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Alle anzeigen ({0})","Button in mod manager. Shows all installed mods."
```

French:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Afficher les mods actifs ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Afficher tous les mods ({0})","Button in mod manager. Shows all installed mods."
```
