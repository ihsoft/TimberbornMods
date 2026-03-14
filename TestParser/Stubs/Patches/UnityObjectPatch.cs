using HarmonyLib;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local

namespace TestParser.Stubs.Patches;

[HarmonyPatch(typeof(UnityEngine.Object))]
static class UnityObjectPatch {
  [HarmonyPrefix]
  [HarmonyPatch("IsNativeObjectAlive")]
  static bool ReportAlwaysActive(out bool __result) {
    __result = true;
    return false;
  }
}
