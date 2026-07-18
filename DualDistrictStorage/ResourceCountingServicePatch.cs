using HarmonyLib;
using Timberborn.ResourceCountingSystem;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(ResourceCountingService))]
static class ResourceCountingServicePatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(ResourceCountingService.GetGlobalResourceCount))]
  static void GetGlobalResourceCount(string goodId, ref ResourceCount __result) {
    var registry = DualDistrictStorageRegistry.Instance;
    if (registry != null) {
      __result = registry.Deduplicate(goodId, __result);
    }
  }
}
