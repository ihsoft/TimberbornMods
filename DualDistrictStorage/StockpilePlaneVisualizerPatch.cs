using HarmonyLib;
using Timberborn.StockpileVisualization;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(StockpilePlaneVisualizer), nameof(StockpilePlaneVisualizer.Initialize))]
static class StockpilePlaneVisualizerPatch {
  static void Postfix(StockpilePlaneVisualizer __instance) {
    var storage = __instance.GetComponent<DualDistrictStorage>();
    if (storage?.OwnsSharedPlaneVisualization == false) {
      __instance.GetComponent<GoodVisualization>().Clear();
    }
  }
}
