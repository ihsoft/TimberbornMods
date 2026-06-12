// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Timberborn.Modding;
using Timberborn.ModdingUI;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

[HarmonyPatch(typeof(ModListView), nameof(ModListView.Initialize))]
static class ModListViewInitializePatch {
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

    UpdateButtonText(__instance, filterButton, false);

    var showOnlyActive = false;
    filterButton.clicked += () => {
      showOnlyActive = !showOnlyActive;
      ApplyFilter(__instance, showOnlyActive);
      UpdateButtonText(__instance, filterButton, showOnlyActive);
    };
  }

  static void ApplyFilter(ModListView modListView, bool showOnlyActive) {
    var modItems = GetModItems(modListView);

    foreach (var (mod, item) in modItems) {
      var visible = !showOnlyActive || ModPlayerPrefsHelper.IsModEnabled(mod);
      item.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
  }

  static void UpdateButtonText(ModListView modListView, Button filterButton, bool showOnlyActive) {
    var modItems = GetModItems(modListView);
    var activeCount = modItems.Keys.Count(ModPlayerPrefsHelper.IsModEnabled);
    var totalCount = modItems.Count;

    filterButton.text = showOnlyActive
      ? $"Show all ({totalCount})"
      : $"Show active ({activeCount}/{totalCount})";
  }

  static Dictionary<Mod, ModItem> GetModItems(ModListView modListView) {
    return AccessTools.FieldRefAccess<ModListView, Dictionary<Mod, ModItem>>(modListView, "_modItems");
  }
}
