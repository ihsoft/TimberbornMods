using HarmonyLib;
using Timberborn.ResourceCountingSystem;

namespace IgorZ.DualDistrictWarehouse;

[HarmonyPatch(typeof(ResourceCountingService))]
static class ResourceCountingServicePatch {
  [HarmonyPostfix]
  [HarmonyPatch(nameof(ResourceCountingService.GetGlobalResourceCount))]
  static void GetGlobalResourceCount(string goodId, ref ResourceCount __result) {
    var registry = DualDistrictWarehouseRegistry.Instance;
    if (registry != null) {
      __result = registry.Deduplicate(goodId, __result);
    }
  }
}
