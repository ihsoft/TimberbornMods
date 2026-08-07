// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class WaterFormClassifier {
  public const string FeatureKey = "water";

  public WaterClassification Analyze(JsonElement world, int width, int height) {
    var water = new WaterMapDecoder().Decode(world, width, height);
    return Analyze(water);
  }

  public WaterClassification Analyze(DecodedWaterMap water) {
    return Classify(water, new WaterFeatureDiagnostics().Analyze(water));
  }

  public WaterClassification Classify(DecodedWaterMap water, WaterFeatureAnalysis features) {
    var lakeTiles = Enumerable.Range(0, water.SurfaceDepths.Length).Count(cell =>
        features.LakeCoreMask[cell] || features.ShallowLakeCoreMask[cell] || features.LakeShoreMask[cell]);
    var riverTiles = features.RiverCandidateTileCount;
    var lakeCount = features.LakeCount + features.ShallowLakeCount;
    return new WaterClassification(
        water.OpenWaterTileCount, water.OpenWaterRatio, GetBroadBoundaryWaterRatio(water), lakeCount,
        GetWaterForm(water.OpenWaterTileCount, lakeCount, lakeTiles, riverTiles));
  }

  public static double GetBroadBoundaryWaterRatio(DecodedWaterMap water) {
    const int requiredInwardDepth = 5;
    var boundaryTiles = water.Width == 1 || water.Height == 1
        ? water.Width * water.Height
        : 2 * water.Width + 2 * water.Height - 4;
    if (boundaryTiles == 0) {
      return 0;
    }
    var waterTiles = 0;
    for (var y = 0; y < water.Height; y++) {
      for (var x = 0; x < water.Width; x++) {
        if ((x == 0 || x == water.Width - 1 || y == 0 || y == water.Height - 1)
            && HasBroadWaterBehindBoundary(x, y)) {
          waterTiles++;
        }
      }
    }
    return (double) waterTiles / boundaryTiles;

    bool HasBroadWaterBehindBoundary(int x, int y) {
      return y == 0 && HasWaterRun(x, y, 0, 1)
          || y == water.Height - 1 && HasWaterRun(x, y, 0, -1)
          || x == 0 && HasWaterRun(x, y, 1, 0)
          || x == water.Width - 1 && HasWaterRun(x, y, -1, 0);
    }

    bool HasWaterRun(int x, int y, int stepX, int stepY) {
      for (var distance = 0; distance < requiredInwardDepth; distance++) {
        var currentX = x + stepX * distance;
        var currentY = y + stepY * distance;
        if (currentX < 0 || currentX >= water.Width || currentY < 0 || currentY >= water.Height
            || water.SurfaceDepths[currentX + currentY * water.Width] <= 0) {
          return false;
        }
      }
      return true;
    }
  }

  public static string GetWaterForm(int openWaterTiles, int lakeCount, int lakeTiles, int riverTiles) {
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
