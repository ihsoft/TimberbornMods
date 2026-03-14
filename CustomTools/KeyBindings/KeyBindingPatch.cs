// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.KeyBindingSystem;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace IgorZ.CustomTools.KeyBindings;

[HarmonyPatch(typeof(KeyBinding))]
static class KeyBindingPatch {
  [HarmonyPrefix]
  [HarmonyPatch(nameof(KeyBinding.UpdatePressedState))]
  static void ScheduleBindingCheck(KeyBinding __instance) {
    KeyBindingInputProcessor.PressedKeyBindings.Add(__instance);
  }
}
