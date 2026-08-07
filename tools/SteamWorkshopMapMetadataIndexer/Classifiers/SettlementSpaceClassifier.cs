// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class SettlementSpaceClassifier {
  public const string FeatureKey = "settlement_space";
  const int MinimumSufficientCoreCount = 8;
  const double DominantShapeShare = 0.35;

  readonly record struct HeightComponent(int Height, IReadOnlyList<int> Cells);
  readonly record struct SettlementRegion(int Height, int Area, int CoreCount, SettlementShape Shape);
  readonly record struct HeightBand(int Area, int CoreCount);

  enum SettlementShape {
    Plain,
    Terrace,
    Plateau,
    Mixed,
  }

  // Accumulates terrain transitions around one candidate region. It stays local
  // because the transition vectors are only meaningful during shape detection.
  sealed class BoundarySummary {
    internal int Higher { get; set; }
    internal int Lower { get; set; }
    internal int Water { get; set; }
    internal int MapEdge { get; set; }
    internal double HigherVectorX { get; set; }
    internal double HigherVectorY { get; set; }
    internal double LowerVectorX { get; set; }
    internal double LowerVectorY { get; set; }
  }

  static readonly (int X, int Y)[] CardinalOffsets = [(-1, 0), (1, 0), (0, -1), (0, 1)];
  static readonly (int X, int Y)[] DiagonalOffsets = [
      (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1),
  ];

  public SettlementSpaceClassification Analyze(DecodedWaterMap map) {
    var heights = SuppressTinyNoise(map.TerrainHeights, map.Width, map.Height);
    var openWater = map.SurfaceDepths.Select(depth => depth > 0).ToArray();
    var requiredRadius = GetRequiredRadius(map.Width, map.Height);
    var regions = FindHeightComponents(heights, openWater, map.Width, map.Height)
        .Select(component => MeasureRegion(
            component, heights, openWater, map.Width, map.Height, requiredRadius))
        .Where(region => region is not null)
        .Cast<SettlementRegion>()
        .ToArray();
    var capacityByShape = regions.GroupBy(region => region.Shape)
        .ToDictionary(group => group.Key, group => group.Sum(region => region.CoreCount));
    var coreCount = capacityByShape.Values.Sum();
    var plainShare = GetShare(capacityByShape, SettlementShape.Plain, coreCount);
    var terraceShare = GetShare(capacityByShape, SettlementShape.Terrace, coreCount);
    var plateauShare = GetShare(capacityByShape, SettlementShape.Plateau, coreCount);
    var mixedShare = GetShare(capacityByShape, SettlementShape.Mixed, coreCount);
    var landArea = checked(map.Width * map.Height) - map.OpenWaterTileCount;
    var dominantBand = GetDominantHeightBand(regions);
    var dominantBandLandRatio = dominantBand.Area / (double)Math.Max(landArea, 1);
    var dominantBandCoreShare = dominantBand.CoreCount / (double)Math.Max(coreCount, 1);
    var spaceType = GetSpaceType(
        coreCount, dominantBandLandRatio, dominantBandCoreShare, plainShare, terraceShare, plateauShare);
    return new SettlementSpaceClassification(
        coreCount, plainShare, terraceShare, plateauShare, mixedShare, spaceType);
  }

  public static string GetSpaceType(
      int coreCount, double dominantBandLandRatio, double dominantBandCoreShare, double plainShare,
      double terraceShare, double plateauShare) {
    if (coreCount < MinimumSufficientCoreCount) {
      return "little_space";
    }
    if (dominantBandLandRatio >= 0.70 && dominantBandCoreShare >= 0.80) {
      return "plain";
    }
    var dominant = new[] {
        (Type: "plain", Share: plainShare),
        (Type: "terraces", Share: terraceShare),
        (Type: "plateau", Share: plateauShare),
    }.MaxBy(item => item.Share);
    return dominant.Share >= DominantShapeShare ? dominant.Type : "much_space";
  }

  static int GetRequiredRadius(int width, int height) {
    var characteristic = Math.Sqrt(checked(width * height));
    return Math.Clamp((int)Math.Round(Math.Log2(characteristic) - 2), 2, 5);
  }

  static double GetShare(
      IReadOnlyDictionary<SettlementShape, int> capacityByShape, SettlementShape shape, int total) {
    return total > 0 ? capacityByShape.GetValueOrDefault(shape) / (double)total : 0;
  }

  static int[] SuppressTinyNoise(int[] source, int width, int height) {
    var replacements = new Dictionary<int, int>();
    for (var cell = 0; cell < source.Length; cell++) {
      var counts = new Dictionary<int, int>();
      foreach (var neighbour in GetNeighbours(cell, width, height, true)) {
        var heightValue = source[neighbour];
        counts[heightValue] = counts.GetValueOrDefault(heightValue) + 1;
      }
      var mode = counts.MaxBy(pair => pair.Value);
      if (Math.Abs(mode.Key - source[cell]) == 1 && mode.Value >= 5) {
        replacements[cell] = mode.Key;
      }
    }

    var accepted = new HashSet<int>();
    var visited = new HashSet<int>();
    foreach (var start in replacements.Keys) {
      if (!visited.Add(start)) {
        continue;
      }
      var component = new List<int>();
      var pending = new Stack<int>([start]);
      while (pending.TryPop(out var cell)) {
        component.Add(cell);
        foreach (var neighbour in GetNeighbours(cell, width, height, false)) {
          if (!visited.Contains(neighbour)
              && replacements.GetValueOrDefault(neighbour, int.MinValue) == replacements[start]
              && source[neighbour] == source[start]) {
            visited.Add(neighbour);
            pending.Push(neighbour);
          }
        }
      }
      if (component.Count <= 2) {
        accepted.UnionWith(component);
      }
    }

    var result = (int[])source.Clone();
    foreach (var cell in accepted) {
      result[cell] = replacements[cell];
    }
    return result;
  }

  static IReadOnlyList<HeightComponent> FindHeightComponents(
      int[] heights, bool[] blocked, int width, int height) {
    var result = new List<HeightComponent>();
    var visited = new bool[heights.Length];
    for (var start = 0; start < heights.Length; start++) {
      if (visited[start] || blocked[start]) {
        continue;
      }
      var level = heights[start];
      var cells = new List<int>();
      var pending = new Stack<int>([start]);
      visited[start] = true;
      while (pending.TryPop(out var cell)) {
        cells.Add(cell);
        foreach (var neighbour in GetNeighbours(cell, width, height, false)) {
          if (!visited[neighbour] && !blocked[neighbour] && heights[neighbour] == level) {
            visited[neighbour] = true;
            pending.Push(neighbour);
          }
        }
      }
      result.Add(new HeightComponent(level, cells));
    }
    return result;
  }

  static SettlementRegion? MeasureRegion(
      HeightComponent component, int[] heights, bool[] openWater,
      int width, int height, int requiredRadius) {
    var cells = component.Cells.ToHashSet();
    var distances = GetInteriorDistances(component.Cells, cells, width, height);
    if (distances.Values.DefaultIfEmpty().Max() < requiredRadius) {
      return null;
    }
    var coreCells = distances.Where(pair => pair.Value >= requiredRadius)
        .OrderByDescending(pair => pair.Value)
        .Select(pair => pair.Key);
    var selected = new List<(int X, int Y)>();
    foreach (var cell in coreCells) {
      var point = (X: cell % width, Y: cell / width);
      if (selected.All(other => Math.Abs(point.X - other.X) + Math.Abs(point.Y - other.Y) >= requiredRadius * 2)) {
        selected.Add(point);
      }
    }
    var shape = ClassifyShape(component, cells, heights, openWater, width, height, requiredRadius);
    return new SettlementRegion(component.Height, component.Cells.Count, selected.Count, shape);
  }

  static Dictionary<int, int> GetInteriorDistances(
      IReadOnlyList<int> component, HashSet<int> cells, int width, int height) {
    var distances = new Dictionary<int, int>();
    var pending = new Queue<int>();
    foreach (var cell in component) {
      var x = cell % width;
      var y = cell / width;
      if (x == 0 || x == width - 1 || y == 0 || y == height - 1
          || GetNeighbours(cell, width, height, false).Any(neighbour => !cells.Contains(neighbour))) {
        distances[cell] = 1;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      foreach (var neighbour in GetNeighbours(cell, width, height, false)) {
        if (cells.Contains(neighbour) && distances.TryAdd(neighbour, distances[cell] + 1)) {
          pending.Enqueue(neighbour);
        }
      }
    }
    return distances;
  }

  static SettlementShape ClassifyShape(
      HeightComponent component, HashSet<int> cells, int[] heights, bool[] openWater,
      int width, int height, int requiredRadius) {
    var centerX = component.Cells.Average(cell => cell % width);
    var centerY = component.Cells.Average(cell => cell / width);
    var boundary = new BoundarySummary();
    foreach (var cell in component.Cells) {
      var x = cell % width;
      var y = cell / width;
      foreach (var (deltaX, deltaY) in CardinalOffsets) {
        var neighbourX = x + deltaX;
        var neighbourY = y + deltaY;
        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height) {
          boundary.MapEdge++;
          continue;
        }
        var neighbour = neighbourX + neighbourY * width;
        if (cells.Contains(neighbour)) {
          continue;
        }
        if (openWater[neighbour]) {
          boundary.Water++;
          continue;
        }
        var vectorX = neighbourX - centerX;
        var vectorY = neighbourY - centerY;
        var length = Math.Sqrt(vectorX * vectorX + vectorY * vectorY);
        if (heights[neighbour] > component.Height) {
          boundary.Higher++;
          boundary.HigherVectorX += vectorX / Math.Max(length, 1);
          boundary.HigherVectorY += vectorY / Math.Max(length, 1);
        } else {
          boundary.Lower++;
          boundary.LowerVectorX += vectorX / Math.Max(length, 1);
          boundary.LowerVectorY += vectorY / Math.Max(length, 1);
        }
      }
    }
    var total = boundary.Higher + boundary.Lower + boundary.Water + boundary.MapEdge;
    var minimumElevationBoundary = Math.Max(requiredRadius * 2, total * 0.15);
    if (boundary.Higher + boundary.Lower < minimumElevationBoundary) {
      return SettlementShape.Plain;
    }
    if (boundary.Lower >= minimumElevationBoundary && boundary.Higher <= total * 0.15) {
      return SettlementShape.Plateau;
    }
    var higherStrength = GetDirectionStrength(
        boundary.HigherVectorX, boundary.HigherVectorY, boundary.Higher);
    var lowerStrength = GetDirectionStrength(boundary.LowerVectorX, boundary.LowerVectorY, boundary.Lower);
    var opposition = GetOpposition(boundary);
    return boundary.Higher >= minimumElevationBoundary && boundary.Lower >= minimumElevationBoundary
        && higherStrength >= 0.25 && lowerStrength >= 0.25 && opposition >= 0.4
        ? SettlementShape.Terrace
        : SettlementShape.Mixed;
  }

  static double GetDirectionStrength(double vectorX, double vectorY, int edgeCount) {
    return edgeCount > 0 ? Math.Sqrt(vectorX * vectorX + vectorY * vectorY) / edgeCount : 0;
  }

  static double GetOpposition(BoundarySummary boundary) {
    var higherLength = Math.Sqrt(
        boundary.HigherVectorX * boundary.HigherVectorX + boundary.HigherVectorY * boundary.HigherVectorY);
    var lowerLength = Math.Sqrt(
        boundary.LowerVectorX * boundary.LowerVectorX + boundary.LowerVectorY * boundary.LowerVectorY);
    if (higherLength == 0 || lowerLength == 0) {
      return 0;
    }
    return -(boundary.HigherVectorX * boundary.LowerVectorX + boundary.HigherVectorY * boundary.LowerVectorY)
      / (higherLength * lowerLength);
  }

  static HeightBand GetDominantHeightBand(IReadOnlyCollection<SettlementRegion> regions) {
    if (regions.Count == 0) {
      return new HeightBand(0, 0);
    }
    var byHeight = regions.GroupBy(region => region.Height)
        .ToDictionary(group => group.Key, group => new HeightBand(
            group.Sum(region => region.Area), group.Sum(region => region.CoreCount)));
    return byHeight.Keys.Select(center => byHeight
        .Where(pair => Math.Abs(pair.Key - center) <= 1)
        .Aggregate(new HeightBand(0, 0), (sum, pair) => new HeightBand(
            sum.Area + pair.Value.Area, sum.CoreCount + pair.Value.CoreCount)))
        .MaxBy(band => (band.Area, band.CoreCount));
  }

  static IEnumerable<int> GetNeighbours(int cell, int width, int height, bool diagonals) {
    var x = cell % width;
    var y = cell / width;
    var offsets = diagonals ? DiagonalOffsets : CardinalOffsets;
    foreach (var (deltaX, deltaY) in offsets) {
      var neighbourX = x + deltaX;
      var neighbourY = y + deltaY;
      if (neighbourX >= 0 && neighbourX < width && neighbourY >= 0 && neighbourY < height) {
        yield return neighbourX + neighbourY * width;
      }
    }
  }
}
