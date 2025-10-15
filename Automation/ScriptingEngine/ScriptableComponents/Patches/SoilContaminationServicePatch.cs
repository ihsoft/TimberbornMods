// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.SoilContaminationSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

[HarmonyPatch(typeof(SoilContaminationService), nameof(SoilContaminationService.SetContaminationLevel))]
static class SoilContaminationServicePatch {
  // ReSharper disable once UnusedMember.Local
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static void Postfix(Vector3Int coordinates) {
    PlantableScriptableComponent.Instance.OnContaminationChanged(coordinates);
  }
}
