using System.Globalization;
using System.Text.Json;

static class WaterMapDecoder {
  const float WaterEpsilon = 0.001f;

  public static DecodedWaterMap Decode(JsonElement world, int width, int height) {
    var singletons = world.GetProperty("Singletons");
    var terrainHeights = DecodeTerrainHeights(singletons.GetProperty("TerrainMap"), width, height);
    var waterMap = singletons.GetProperty("WaterMapNew");
    var levels = waterMap.GetProperty("Levels").GetInt32();
    var tokens = SplitPackedArray(waterMap.GetProperty("WaterColumns"));
    var area = checked(width * height);
    if (levels < 1 || tokens.Length != checked(area * levels)) {
      throw new InvalidDataException(
          $"WaterColumns has {tokens.Length} values for {width}x{height} and {levels} levels.");
    }

    var surfaceDepths = new float[area];
    var surfaceFloors = new int[area];
    var surfaceLevels = Enumerable.Repeat(-1, area).ToArray();
    var undergroundWaterColumns = 0;
    for (var cell = 0; cell < area; cell++) {
      surfaceFloors[cell] = terrainHeights[cell];
      for (var level = 0; level < levels; level++) {
        var column = ParseWaterColumn(tokens[cell + level * area]);
        if (column.Depth <= WaterEpsilon) {
          continue;
        }
        if (column.Floor < terrainHeights[cell]) {
          undergroundWaterColumns++;
        } else if (column.Floor == terrainHeights[cell]) {
          surfaceDepths[cell] = Math.Max(surfaceDepths[cell], column.Depth);
          surfaceLevels[cell] = level;
        }
      }
    }
    var surfaceFlows = DecodeSurfaceFlows(waterMap, surfaceLevels, width, height, levels);
    return new DecodedWaterMap(width, height, terrainHeights, surfaceFloors, surfaceDepths,
        surfaceFlows.Magnitudes, surfaceFlows.Coherences, surfaceFlows.Edges, undergroundWaterColumns, levels);
  }

  static DecodedFlows DecodeSurfaceFlows(
      JsonElement waterMap, int[] surfaceLevels, int width, int height, int levels) {
    var area = checked(width * height);
    var magnitudes = new float[area];
    var coherences = new float[area];
    var edges = new List<SurfaceFlowEdge>();
    if (!waterMap.TryGetProperty("ColumnOutflows", out var packedOutflows)) {
      return new DecodedFlows(magnitudes, coherences, edges);
    }
    var tokens = SplitPackedArray(packedOutflows);
    if (tokens.Length != checked(area * levels)) {
      throw new InvalidDataException(
          $"ColumnOutflows has {tokens.Length} values; expected {area * levels}.");
    }
    for (var cell = 0; cell < area; cell++) {
      var level = surfaceLevels[cell];
      if (level < 0) {
        continue;
      }
      var token = tokens[cell + level * area];
      if (token == "0") {
        continue;
      }
      var parts = token.Split(':');
      double vectorX = 0;
      double vectorY = 0;
      for (var index = 0; index < parts.Length; index++) {
        if (!TryParseTargetedFlow(parts[index], out var targetIndex, out var flow)) {
          continue;
        }
        magnitudes[cell] += Math.Abs(flow);
        if (TryGetTargetCell(targetIndex, width, height, out var targetCell) && targetCell != cell && flow > 0) {
          edges.Add(new SurfaceFlowEdge(cell, targetCell, flow));
        }
        var direction = index switch {
            0 => (X: 0d, Y: -1d),
            1 => (X: -1d, Y: 0d),
            2 => (X: 0d, Y: 1d),
            3 => (X: 1d, Y: 0d),
            _ => GetTargetDirection(cell, targetIndex, width, height),
        };
        vectorX += direction.X * flow;
        vectorY += direction.Y * flow;
      }
      if (magnitudes[cell] > 0) {
        coherences[cell] = (float) Math.Clamp(
            Math.Sqrt(vectorX * vectorX + vectorY * vectorY) / magnitudes[cell], 0, 1);
      }
    }
    return new DecodedFlows(magnitudes, coherences, edges);
  }

  static bool TryParseTargetedFlow(string value, out int targetIndex, out float flow) {
    var separator = value.IndexOf('|');
    if (separator < 0) {
      targetIndex = -1;
      flow = 0;
      return false;
    }
    targetIndex = int.Parse(value[..separator], CultureInfo.InvariantCulture);
    flow = float.Parse(value[(separator + 1)..], CultureInfo.InvariantCulture);
    return true;
  }

  static (double X, double Y) GetTargetDirection(int cell, int targetIndex, int width, int height) {
    var sourceX = cell % width;
    var sourceY = cell / width;
    var stride = width + 2;
    var verticalStride = checked(stride * (height + 2));
    var flatTarget = targetIndex % verticalStride;
    var targetX = flatTarget % stride - 1;
    var targetY = flatTarget / stride - 1;
    var deltaX = targetX - sourceX;
    var deltaY = targetY - sourceY;
    var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    return length > 0 ? (deltaX / length, deltaY / length) : (0, 0);
  }

  static bool TryGetTargetCell(int targetIndex, int width, int height, out int targetCell) {
    var stride = width + 2;
    var verticalStride = checked(stride * (height + 2));
    var flatTarget = targetIndex % verticalStride;
    var targetX = flatTarget % stride - 1;
    var targetY = flatTarget / stride - 1;
    if (targetX < 0 || targetX >= width || targetY < 0 || targetY >= height) {
      targetCell = -1;
      return false;
    }
    targetCell = targetX + targetY * width;
    return true;
  }

  static int[] DecodeTerrainHeights(JsonElement terrainMap, int width, int height) {
    var area = checked(width * height);
    if (terrainMap.TryGetProperty("Heights", out var heightsElement)) {
      var tokens = SplitPackedArray(heightsElement);
      if (tokens.Length != area) {
        throw new InvalidDataException($"Terrain Heights has {tokens.Length} values; expected {area}.");
      }
      return tokens.Select(int.Parse).ToArray();
    }
    if (!terrainMap.TryGetProperty("Voxels", out var voxelsElement)) {
      throw new InvalidDataException("TerrainMap has neither Heights nor Voxels.");
    }

    var voxels = SplitPackedArray(voxelsElement);
    if (voxels.Length == 0 || voxels.Length % area != 0) {
      throw new InvalidDataException($"Terrain Voxels has {voxels.Length} values; expected a multiple of {area}.");
    }
    var heights = new int[area];
    var terrainLevels = voxels.Length / area;
    for (var z = 0; z < terrainLevels; z++) {
      for (var cell = 0; cell < area; cell++) {
        if (voxels[cell + z * area] == "1") {
          heights[cell] = z + 1;
        }
      }
    }
    return heights;
  }

  static string[] SplitPackedArray(JsonElement packedList) {
    var value = packedList.GetProperty("Array").GetString()
        ?? throw new InvalidDataException("Packed Array is null.");
    return value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
  }

  static WaterColumnValue ParseWaterColumn(string value) {
    if (value == "0") {
      return default;
    }
    var parts = value.Split(':');
    if (parts.Length < 4) {
      throw new InvalidDataException($"Invalid WaterColumn value: {value}");
    }
    return new WaterColumnValue(
        float.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[3], CultureInfo.InvariantCulture));
  }
}

sealed record DecodedWaterMap(
    int Width, int Height, int[] TerrainHeights, int[] SurfaceFloors, float[] SurfaceDepths,
    float[] SurfaceFlowMagnitudes, float[] SurfaceFlowCoherences, IReadOnlyList<SurfaceFlowEdge> SurfaceFlowEdges,
    int UndergroundWaterColumnCount, int SerializedLevels) {
  public int OpenWaterTileCount => SurfaceDepths.Count(depth => depth > 0);
  public double OpenWaterRatio => (double) OpenWaterTileCount / (Width * Height);
  public float MaximumSurfaceDepth => SurfaceDepths.Length == 0 ? 0 : SurfaceDepths.Max();
  public float MaximumSurfaceFlow => SurfaceFlowMagnitudes.Length == 0 ? 0 : SurfaceFlowMagnitudes.Max();
}

readonly record struct WaterColumnValue(float Depth, int Floor);

readonly record struct SurfaceFlowEdge(int SourceCell, int TargetCell, float Flow);

readonly record struct DecodedFlows(
    float[] Magnitudes, float[] Coherences, IReadOnlyList<SurfaceFlowEdge> Edges);
