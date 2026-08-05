static class WaterFeatureDiagnostics {
  public static WaterFeatureAnalysis Analyze(DecodedWaterMap map) {
    var openWater = map.SurfaceDepths.Select(depth => depth > 0).ToArray();
    var shallowWater = map.SurfaceDepths.Select(depth => depth > 0 && depth <= 2).ToArray();
    var distanceToShore = CalculateDistanceToShore(map.SurfaceDepths, map.Width, map.Height);
    var broadWater = openWater.Select((value, cell) => value && distanceToShore[cell] >= 3).ToArray();
    var lakeCore = new bool[openWater.Length];
    var shallowLakeCore = new bool[openWater.Length];
    var ambiguousBroadWater = new bool[openWater.Length];
    var broadRegionHydrology = new List<BroadWaterHydrology>();
    var lakeCount = 0;
    var shallowLakeCount = 0;
    var surfaceElevations = map.SurfaceFloors.Zip(
        map.SurfaceDepths, (floor, depth) => floor + depth).ToArray();
    foreach (var component in FindSurfaceLevelComponents(
        broadWater, surfaceElevations, map.Width, map.Height, 0.25f)) {
      var minX = component.Min(cell => cell % map.Width);
      var maxX = component.Max(cell => cell % map.Width);
      var minY = component.Min(cell => cell / map.Width);
      var maxY = component.Max(cell => cell / map.Width);
      var spanX = maxX - minX + 1;
      var spanY = maxY - minY + 1;
      var medianFlowCoherence = component.Select(cell => map.SurfaceFlowCoherences[cell]).Order().ToArray()
          [component.Count / 2];
      var surfaceHeights = component.Select(cell => map.SurfaceFloors[cell] + map.SurfaceDepths[cell])
          .Order().ToArray();
      var surfaceHeightSpread = surfaceHeights[(int) ((surfaceHeights.Length - 1) * 0.9)]
          - surfaceHeights[(int) ((surfaceHeights.Length - 1) * 0.1)];
      var cells = component.ToHashSet();
      var boundaryEdges = component.Sum(cell => {
        var x = cell % map.Width;
        var y = cell / map.Width;
        var edges = 0;
        edges += x == 0 || !cells.Contains(cell - 1) ? 1 : 0;
        edges += x == map.Width - 1 || !cells.Contains(cell + 1) ? 1 : 0;
        edges += y == 0 || !cells.Contains(cell - map.Width) ? 1 : 0;
        edges += y == map.Height - 1 || !cells.Contains(cell + map.Width) ? 1 : 0;
        return edges;
      });
      var compactness = 4 * Math.PI * component.Count / (boundaryEdges * boundaryEdges);
      var maximumShoreDistance = component.Max(cell => distanceToShore[cell]);
      var innerCoreTiles = component.Count(cell => distanceToShore[cell] >= 5);
      var inflow = map.SurfaceFlowEdges.Where(edge => !cells.Contains(edge.SourceCell)
          && cells.Contains(edge.TargetCell)).Sum(edge => edge.Flow);
      var outflow = map.SurfaceFlowEdges.Where(edge => cells.Contains(edge.SourceCell)
          && !cells.Contains(edge.TargetCell)).Sum(edge => edge.Flow);
      var volume = component.Sum(cell => map.SurfaceDepths[cell]);
      var throughput = Math.Min(inflow, outflow);
      var throughputPerVolume = volume > 0 ? throughput / volume : 0;
      broadRegionHydrology.Add(new BroadWaterHydrology(
          component.Count, spanX, spanY,
          component.Average(cell => cell % map.Width), component.Average(cell => cell / map.Width),
          boundaryEdges, compactness, maximumShoreDistance, innerCoreTiles,
          volume, inflow, outflow, throughputPerVolume,
          medianFlowCoherence, surfaceHeightSpread));
      var innerCoreRatio = (double) innerCoreTiles / component.Count;
      var shapeSupportsBasin = compactness >= 0.22
          && (component.Count < 100 || innerCoreRatio >= 0.49 || compactness >= 0.45)
          || innerCoreRatio >= 0.7;
      var flowSupportsBasin = medianFlowCoherence <= 0.8f && throughputPerVolume <= 0.06
          || medianFlowCoherence <= 0.93f && throughputPerVolume <= 0.03 && compactness >= 0.4;
      var confident = component.Count >= 9 && Math.Min(spanX, spanY) >= 3
          && (double) Math.Max(spanX, spanY) / Math.Min(spanX, spanY) <= 4
          && shapeSupportsBasin && flowSupportsBasin && surfaceHeightSpread <= 0.25f;
      var confidentRiver = !confident && (surfaceHeightSpread > 0.25f
          || throughputPerVolume > 0.1 || medianFlowCoherence > 0.8f
          || (double) Math.Max(spanX, spanY) / Math.Min(spanX, spanY) > 4);
      var deep = component.Any(cell => map.SurfaceDepths[cell] >= 3);
      if (confident) {
        if (deep) {
          lakeCount++;
        } else {
          shallowLakeCount++;
        }
      }
      foreach (var cell in component) {
        if (confident) {
          (deep ? lakeCore : shallowLakeCore)[cell] = true;
        } else if (!confidentRiver) {
          ambiguousBroadWater[cell] = true;
        }
      }
    }
    var combinedLakeCore = lakeCore.Zip(shallowLakeCore, (deep, shallow) => deep || shallow).ToArray();
    var lakeShore = FindLakeShore(openWater, combinedLakeCore, map.Width, map.Height);
    var riverCandidates = openWater.Select((value, cell) => value
        && !lakeShore[cell] && !shallowLakeCore[cell] && !ambiguousBroadWater[cell]).ToArray();
    foreach (var component in FindComponents(riverCandidates, map.Width, map.Height)) {
      var minX = component.Min(cell => cell % map.Width);
      var maxX = component.Max(cell => cell % map.Width);
      var minY = component.Min(cell => cell / map.Width);
      var maxY = component.Max(cell => cell / map.Width);
      if (component.Count >= 10 && Math.Max(maxX - minX + 1, maxY - minY + 1) >= 5) {
        continue;
      }
      foreach (var cell in component) {
        riverCandidates[cell] = false;
      }
    }
    return new WaterFeatureAnalysis(
        lakeCore, shallowLakeCore, ambiguousBroadWater, shallowWater, lakeShore, riverCandidates,
        lakeCount, shallowLakeCount, lakeCore.Count(value => value), shallowLakeCore.Count(value => value),
        ambiguousBroadWater.Count(value => value), shallowWater.Count(value => value),
        riverCandidates.Count(value => value), broadRegionHydrology);
  }

  static int[] CalculateDistanceToShore(float[] depths, int width, int height) {
    var distances = Enumerable.Repeat(int.MaxValue, depths.Length).ToArray();
    var pending = new Queue<int>();
    for (var cell = 0; cell < depths.Length; cell++) {
      var x = cell % width;
      var y = cell / width;
      if (depths[cell] <= 0 || x == 0 || x == width - 1 || y == 0 || y == height - 1) {
        distances[cell] = depths[cell] > 0 ? 1 : 0;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      var x = cell % width;
      var y = cell / width;
      Visit(x - 1, y);
      Visit(x + 1, y);
      Visit(x, y - 1);
      Visit(x, y + 1);

      void Visit(int neighbourX, int neighbourY) {
        if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height) {
          return;
        }
        var neighbour = neighbourX + neighbourY * width;
        if (distances[neighbour] > distances[cell] + 1) {
          distances[neighbour] = distances[cell] + 1;
          pending.Enqueue(neighbour);
        }
      }
    }
    return distances;
  }

  static bool[] FindLakeShore(bool[] shallowWater, bool[] lakeCore, int width, int height) {
    var shore = new bool[lakeCore.Length];
    for (var cell = 0; cell < lakeCore.Length; cell++) {
      if (!lakeCore[cell]) {
        continue;
      }
      var centerX = cell % width;
      var centerY = cell / width;
      for (var deltaY = -2; deltaY <= 2; deltaY++) {
        for (var deltaX = -2; deltaX <= 2; deltaX++) {
          var x = centerX + deltaX;
          var y = centerY + deltaY;
          if (x >= 0 && x < width && y >= 0 && y < height) {
            var neighbour = x + y * width;
            shore[neighbour] |= shallowWater[neighbour] && !lakeCore[neighbour];
          }
        }
      }
    }
    return shore;
  }

  static IReadOnlyList<List<int>> FindComponents(bool[] mask, int width, int height) {
    var components = new List<List<int>>();
    var visited = new bool[mask.Length];
    for (var start = 0; start < mask.Length; start++) {
      if (!mask[start] || visited[start]) {
        continue;
      }
      var component = new List<int>();
      var pending = new Queue<int>();
      pending.Enqueue(start);
      visited[start] = true;
      while (pending.TryDequeue(out var cell)) {
        component.Add(cell);
        var x = cell % width;
        var y = cell / width;
        Visit(x - 1, y);
        Visit(x + 1, y);
        Visit(x, y - 1);
        Visit(x, y + 1);
      }
      components.Add(component);

      void Visit(int x, int y) {
        if (x < 0 || x >= width || y < 0 || y >= height) {
          return;
        }
        var neighbour = x + y * width;
        if (mask[neighbour] && !visited[neighbour]) {
          visited[neighbour] = true;
          pending.Enqueue(neighbour);
        }
      }
    }
    return components;
  }

  static IReadOnlyList<List<int>> FindSurfaceLevelComponents(
      bool[] mask, float[] surfaceElevations, int width, int height, float maximumSpread) {
    var components = new List<List<int>>();
    var visited = new bool[mask.Length];
    for (var start = 0; start < mask.Length; start++) {
      if (!mask[start] || visited[start]) {
        continue;
      }
      var component = new List<int>();
      var pending = new Queue<int>();
      var minimumElevation = surfaceElevations[start];
      var maximumElevation = surfaceElevations[start];
      pending.Enqueue(start);
      visited[start] = true;
      while (pending.TryDequeue(out var cell)) {
        component.Add(cell);
        var x = cell % width;
        var y = cell / width;
        Visit(x - 1, y);
        Visit(x + 1, y);
        Visit(x, y - 1);
        Visit(x, y + 1);
      }
      components.Add(component);

      void Visit(int x, int y) {
        if (x < 0 || x >= width || y < 0 || y >= height) {
          return;
        }
        var neighbour = x + y * width;
        if (!mask[neighbour] || visited[neighbour]) {
          return;
        }
        var elevation = surfaceElevations[neighbour];
        var nextMinimum = Math.Min(minimumElevation, elevation);
        var nextMaximum = Math.Max(maximumElevation, elevation);
        if (nextMaximum - nextMinimum > maximumSpread) {
          return;
        }
        minimumElevation = nextMinimum;
        maximumElevation = nextMaximum;
        visited[neighbour] = true;
        pending.Enqueue(neighbour);
      }
    }
    return components;
  }
}

sealed record WaterFeatureAnalysis(
    bool[] LakeCoreMask, bool[] ShallowLakeCoreMask, bool[] AmbiguousBroadWaterMask, bool[] ShallowWaterMask,
    bool[] LakeShoreMask, bool[] RiverCandidateMask, int LakeCount, int ShallowLakeCount, int LakeCoreTileCount,
    int ShallowLakeCoreTileCount, int AmbiguousBroadWaterTileCount, int ShallowWaterTileCount,
    int RiverCandidateTileCount, IReadOnlyList<BroadWaterHydrology> BroadRegionHydrology);

sealed record BroadWaterHydrology(
    int CoreTiles, int SpanX, int SpanY, double CenterX, double CenterY,
    int BoundaryEdges, double Compactness, int MaximumShoreDistance, int InnerCoreTiles,
    double Volume, double Inflow, double Outflow, double ThroughputPerVolume,
    double MedianFlowCoherence, double SurfaceHeightSpread);
