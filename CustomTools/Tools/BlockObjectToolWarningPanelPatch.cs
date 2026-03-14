// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.BlockObjectToolsUI;
using Timberborn.ToolSystem;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.CustomTools.Tools;

[HarmonyPatch(typeof(BlockObjectToolWarningPanel))]
static class BlockObjectToolWarningPanelPatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(BlockObjectToolWarningPanel.UpdateSingleton))]
  static void ShowCustomToolWarning(BlockObjectToolWarningPanel __instance, ToolService ____toolService) {
    if (____toolService.ActiveTool is AbstractCustomTool activeTool) {
      __instance.UpdateText(activeTool.GetWarningText());
    }
  }
}
