// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.MainMenuModdingUI;
using Timberborn.Modding;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.ModUIPatches;

[HarmonyPatch(typeof(ModManagerBox), nameof(ModManagerBox.Open))]
static class ModManagerBoxOpenPatch {
  static void Postfix(ModManagerBox __instance) {
    foreach (var modItem in __instance._modListView._modItems.Values) {
      var toggle = modItem.Root.Q<Toggle>("ModToggle");
      toggle?.SetValueWithoutNotify(ModPlayerPrefsHelper.IsModEnabled(modItem.Mod));
    }
    __instance._modListView._modWarningUpdater.Update(__instance._modListView._modItems);
  }
}
