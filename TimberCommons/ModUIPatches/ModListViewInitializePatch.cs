// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using HarmonyLib;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.ModdingUI;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.ModUIPatches;

[HarmonyPatch(typeof(ModListView), nameof(ModListView.Initialize))]
static class ModListViewInitializePatch {
  const string ShowActiveModsLocKey = "IgorZ.TimberCommons.ModUIPatches.ModListView.ShowActiveMods";
  const string ShowAllModsLocKey = "IgorZ.TimberCommons.ModUIPatches.ModListView.ShowAllMods";

  static ILoc _loc = null!;

  public static void SetLoc(ILoc loc) {
    _loc = loc;
  }

  static void Postfix(ModListView __instance, VisualElement root) {
    var resetButton = root.Q<Button>("ResetOrderButton");
    if (resetButton?.parent == null) {
      return;
    }

    if (root.Q<Button>("ShowActiveModsButton") != null) {
      return;
    }

    var filterButton = new Button() {
        name = "ShowActiveModsButton",
    };

    foreach (var className in resetButton.GetClasses()) {
      filterButton.AddToClassList(className);
    }

    resetButton.parent.Insert(resetButton.parent.IndexOf(resetButton) + 1, filterButton);

    var showOnlyActive = false;
    UpdateButtonText(__instance, filterButton, showOnlyActive);

    filterButton.clicked += () => {
      showOnlyActive = !showOnlyActive;
      ApplyFilter(__instance, showOnlyActive);
      UpdateButtonText(__instance, filterButton, showOnlyActive);
    };
  }

  static void ApplyFilter(ModListView modListView, bool showOnlyActive) {
    foreach (var (mod, item) in modListView._modItems) {
      var visible = !showOnlyActive || ModPlayerPrefsHelper.IsModEnabled(mod);
      item.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }

  static void UpdateButtonText(ModListView modListView, Button filterButton, bool showOnlyActive) {
    var activeCount = modListView._modItems.Keys.Count(ModPlayerPrefsHelper.IsModEnabled);
    var totalCount = modListView._modItems.Count;

    filterButton.text = showOnlyActive
        ? _loc.T(ShowAllModsLocKey, totalCount)
        : _loc.T(ShowActiveModsLocKey, activeCount, totalCount);
  }
}
