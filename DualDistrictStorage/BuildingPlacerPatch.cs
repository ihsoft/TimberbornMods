using HarmonyLib;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(BuildingPlacer), nameof(BuildingPlacer.CanHandle))]
static class BuildingPlacerPatch {
  static void Postfix(BlockObjectSpec template, ref bool __result) {
    if (__result && template.HasSpec<AsymmetricDualDistrictStoragePlacerSpec>()) {
      __result = false;
    }
  }
}
