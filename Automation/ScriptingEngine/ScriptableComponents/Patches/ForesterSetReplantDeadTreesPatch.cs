// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.Forestry;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

/// <summary>Triggers plat spots updates in the platable signal.</summary>
[HarmonyPatch(typeof(Forester), nameof(Forester.SetReplantDeadTrees))]
static class ForesterSetReplantDeadTreesPatch {
  // ReSharper disable once UnusedMember.Local
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static void Postfix(Forester __instance) {
    PlantableScriptableComponent.Instance.OnForesterSettingsChanged(__instance);
  }
}
