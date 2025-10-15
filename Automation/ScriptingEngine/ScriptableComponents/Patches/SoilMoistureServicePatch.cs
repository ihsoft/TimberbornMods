// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Timberborn.SoilMoistureSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

[HarmonyPatch(typeof(SoilMoistureService), nameof(SoilMoistureService.SetMoistureLevel))]
static class SoilMoistureServicePatch {
  // ReSharper disable once UnusedMember.Local
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static void Postfix(Vector3Int coordinates) {
    PlantableScriptableComponent.Instance.OnMoistureChanged(coordinates);
  }
}
