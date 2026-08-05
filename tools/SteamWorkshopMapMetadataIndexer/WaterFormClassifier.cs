using System.Text.Json;
using System.Text.Json.Serialization;

static class WaterFormClassifier {
  public const string FeatureKey = "water";

  public static WaterClassificationResult Analyze(JsonElement world, int width, int height) {
    var water = WaterMapDecoder.Decode(world, width, height);
    return Analyze(water);
  }

  public static WaterClassificationResult Analyze(DecodedWaterMap water) {
    return Classify(water, WaterFeatureDiagnostics.Analyze(water));
  }

  public static WaterClassificationResult Classify(DecodedWaterMap water, WaterFeatureAnalysis features) {
    var lakeTiles = Enumerable.Range(0, water.SurfaceDepths.Length).Count(cell =>
        features.LakeCoreMask[cell] || features.ShallowLakeCoreMask[cell] || features.LakeShoreMask[cell]);
    var riverTiles = features.RiverCandidateTileCount;
    var lakeCount = features.LakeCount + features.ShallowLakeCount;
    return new WaterClassificationResult(
        water.OpenWaterTileCount, water.OpenWaterRatio, lakeCount,
        GetWaterForm(water.OpenWaterTileCount, lakeCount, lakeTiles, riverTiles));
  }

  internal static string GetWaterForm(int openWaterTiles, int lakeCount, int lakeTiles, int riverTiles) {
    if (openWaterTiles == 0) {
      return "none";
    }
    if (lakeCount == 0 || lakeTiles < openWaterTiles * 0.15) {
      return "rivers";
    }
    if (riverTiles < openWaterTiles * 0.15) {
      return "lakes";
    }
    if (lakeTiles >= riverTiles * 3) {
      return "lakes";
    }
    if (riverTiles >= lakeTiles * 3) {
      return "rivers";
    }
    return "rivers_and_lakes";
  }
}

sealed record WaterClassificationResult(
    [property: JsonPropertyName("open_water_tiles")] int OpenWaterTiles,
    [property: JsonPropertyName("open_water_ratio")] double OpenWaterRatio,
    [property: JsonPropertyName("lake_count")] int LakeCount,
    [property: JsonPropertyName("water_form")] string WaterForm);
