using HarmonyLib;
using Timberborn.StockpileVisualization;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(StockpileGoodPileVisualizer), nameof(StockpileGoodPileVisualizer.UpdateAmount))]
static class StockpileGoodPileVisualizerPatch {
  static void Prefix(StockpileGoodPileVisualizer __instance, ref int amountInStock) {
    var storage = __instance.GetComponent<DualDistrictStorage>();
    if (storage?.VisualizationShareDenominator > 0) {
      amountInStock = storage.VisualizedAmount(amountInStock);
    }
  }
}
