using System.Text.Json.Serialization;

static class PlateauClassifier {
  public const string FeatureKey = "plateaus";

  public static PlateauClassificationResult Analyze(DecodedWaterMap map) {
    var heights = SuppressTinyNoise(map.TerrainHeights, map.Width, map.Height);
    var openWater = map.SurfaceDepths.Select(depth => depth > 0).ToArray();
    var requiredRadius = GetRequiredRadius(map.Width, map.Height);
    var plateaus = FindHeightComponents(heights, openWater, map.Width, map.Height)
        .Select(component => MeasurePlateau(component, map.Width, map.Height, requiredRadius))
        .Where(plateau => plateau is not null)
        .Cast<Plateau>()
        .ToArray();
    var landArea = checked(map.Width * map.Height) - map.OpenWaterTileCount;
    var plateauArea = plateaus.Sum(plateau => plateau.Area);
    var plateauLandRatio = landArea > 0 ? (double) plateauArea / landArea : 0;
    var dominantBandLandRatio = GetDominantHeightBandArea(plateaus) / (double) Math.Max(landArea, 1);
    var level = GetLevel(plateauLandRatio, dominantBandLandRatio);
    return new PlateauClassificationResult(plateaus.Length, plateauLandRatio, level);
  }

  internal static string GetLevel(double plateauLandRatio, double dominantBandLandRatio) {
    if (dominantBandLandRatio >= 0.80
        || dominantBandLandRatio >= 0.70 && plateauLandRatio >= 0.85) {
      return "flat_map";
    }
    if (plateauLandRatio >= 0.45) {
      return "many_plateaus";
    }
    if (plateauLandRatio >= 0.25) {
      return "has_plateaus";
    }
    return "few_plateaus";
  }

  internal static int GetRequiredRadius(int width, int height) {
    var characteristic = Math.Sqrt(checked(width * height));
    return Math.Clamp((int) Math.Round(Math.Log2(characteristic) - 2), 2, 5);
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

    var result = (int[]) source.Clone();
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

  static Plateau? MeasurePlateau(HeightComponent component, int width, int height, int requiredRadius) {
    var cells = component.Cells.ToHashSet();
    var distances = new Dictionary<int, int>();
    var pending = new Queue<int>();
    foreach (var cell in component.Cells) {
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
    return distances.Values.DefaultIfEmpty().Max() >= requiredRadius
        ? new Plateau(component.Height, component.Cells.Count)
        : null;
  }

  static int GetDominantHeightBandArea(IEnumerable<Plateau> plateaus) {
    var byHeight = plateaus.GroupBy(plateau => plateau.Height)
        .ToDictionary(group => group.Key, group => group.Sum(plateau => plateau.Area));
    return byHeight.Keys.Select(center => byHeight
        .Where(pair => Math.Abs(pair.Key - center) <= 1)
        .Sum(pair => pair.Value)).DefaultIfEmpty().Max();
  }

  static IEnumerable<int> GetNeighbours(int cell, int width, int height, bool diagonals) {
    var x = cell % width;
    var y = cell / width;
    var offsets = diagonals
        ? new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1) }
        : new[] { (-1, 0), (1, 0), (0, -1), (0, 1) };
    foreach (var (deltaX, deltaY) in offsets) {
      var neighbourX = x + deltaX;
      var neighbourY = y + deltaY;
      if (neighbourX >= 0 && neighbourX < width && neighbourY >= 0 && neighbourY < height) {
        yield return neighbourX + neighbourY * width;
      }
    }
  }
}

sealed record PlateauClassificationResult(
    [property: JsonPropertyName("plateau_count")] int PlateauCount,
    [property: JsonPropertyName("plateau_land_ratio")] double PlateauLandRatio,
    [property: JsonPropertyName("plateau_level")] string PlateauLevel);

readonly record struct HeightComponent(int Height, IReadOnlyList<int> Cells);

readonly record struct Plateau(int Height, int Area);
