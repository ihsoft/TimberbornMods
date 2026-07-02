// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.GameSaveRepositorySystemUI;

// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

[HarmonyPatch(typeof(LoadGameBox))]
static class LoadGameBoxPatch {
  [HarmonyPatch(nameof(LoadGameBox.OnSaveSelectionChanged))]
  [HarmonyPostfix]
  static void OnSaveSelectionChangedPostfix() {
    GameSaveVersionLabelUpdater.UpdateCurrent();
  }
}
