// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class IslandClassifier {
  /// <summary>Stable public-index key for projected island areas.</summary>
  public const string FeatureKey = "islands";
  const int MaximumNarrowRiverWidth = 5;
  const double MinimumBoundaryWaterBodyRatio = 0.2;
  const double MinimumDominantShorelineRatio = 0.4;
  const double MinimumExternalWaterBoundaryRatio = 0.2;

  sealed record WaterBodyMeasurement(int Index, double MapBoundaryRatio);

  sealed class IslandAnalyzer {
    sealed record ParentMeasurement(
        int Index, int DryArea, bool TouchesBoundary, int BoundarySideCount,
        bool TouchesOppositeBoundarySides, int MaximumInteriorRadius,
        double ExternalWaterBoundaryRatio, IReadOnlyList<int> Cells);

    readonly DecodedWaterMap _map;
    readonly WaterFeatureAnalysis _waterFeatures;
    readonly bool[] _openWater;
    readonly bool[] _externalWater;
    readonly IReadOnlyList<List<int>> _dryComponents;
    readonly int _requiredCoreRadius;

    public IslandAnalyzer(DecodedWaterMap map, WaterFeatureAnalysis waterFeatures) {
      _map = map;
      _waterFeatures = waterFeatures;
      _openWater = map.SurfaceDepths.Select(value => value > 0).ToArray();
      var outletScale = GetExternalWaterOutletScale(map.Width, map.Height);
      var gatedExternalWater = FindExternalWater(_openWater, map.Width, map.Height, outletScale);
      var waterComponents = FindComponents(_openWater, map.Width, map.Height);
      var waterBodies = waterComponents
          .Select((cells, index) => MeasureWaterBody(index, cells))
          .ToList();
      _externalWater = KeepOpenBoundaryWater(gatedExternalWater, waterComponents, waterBodies);
      _dryComponents = FindComponents(_openWater.Select(value => !value).ToArray(), map.Width, map.Height);
      _requiredCoreRadius = GetRequiredCoreRadius(map.Width, map.Height);
    }

    public IReadOnlyList<int> Analyze() {
      var parentComponents = MergeAcrossRiverComponents(
          _dryComponents, _waterFeatures.RiverCandidateMask, _externalWater,
          _map.Width, _map.Height, _requiredCoreRadius, MaximumNarrowRiverWidth);
      var parents = parentComponents.Select((cells, index) => MeasureParent(index, cells)).ToList();
      var dominantShorelineRatios = MeasureDominantShorelineRatios(
          FindComponents(_openWater, _map.Width, _map.Height), parents);
      return parents
          .Select(parent => (Original: parent, Trimmed: TrimBoundaryContamination(parent)))
          .Where(candidate => IsIslandFamily(candidate.Original, candidate.Trimmed, dominantShorelineRatios))
          .Select(candidate => candidate.Trimmed.DryArea)
          .OrderByDescending(area => area)
          .ToList();
    }

    ParentMeasurement TrimBoundaryContamination(ParentMeasurement family) {
      if (!family.TouchesBoundary
          || family.ExternalWaterBoundaryRatio >= MinimumExternalWaterBoundaryRatio) {
        return family;
      }
      var familyCells = family.Cells.ToHashSet();
      var enclosedComponents = _dryComponents
          .Where(component => familyCells.Contains(component[0])
            && !TouchesBoundary(component)
            && GetMaximumInteriorRadius(component, _map.Width, _map.Height) >= _requiredCoreRadius)
          .ToList();
      return enclosedComponents.Count == 0
          ? family
          : MeasureParent(family.Index, enclosedComponents.SelectMany(component => component).ToList());
    }

    bool IsIslandFamily(
        ParentMeasurement parent, ParentMeasurement trimmed,
        IReadOnlyDictionary<int, double> dominantShorelineRatios) {
      if (parent.MaximumInteriorRadius < _requiredCoreRadius) {
        return false;
      }
      if (!parent.TouchesBoundary) {
        return true;
      }
      if (parent.BoundarySideCount > 2 || parent.TouchesOppositeBoundarySides) {
        return false;
      }
      if (parent.ExternalWaterBoundaryRatio >= MinimumExternalWaterBoundaryRatio) {
        return true;
      }
      // A land family merged through rivers may touch the frame only through unrelated mainland fragments. The
      // shoreline fallback is valid only when trimming those fragments leaves a useful enclosed island behind.
      return dominantShorelineRatios.GetValueOrDefault(parent.Index) >= MinimumDominantShorelineRatio
          && !trimmed.TouchesBoundary
          && trimmed.MaximumInteriorRadius >= _requiredCoreRadius;
    }

    ParentMeasurement MeasureParent(int index, IReadOnlyList<int> cells) {
      var dryCells = cells.Where(cell => !_openWater[cell]).ToList();
      var waterEdges = 0;
      var externalWaterEdges = 0;
      var mapEdges = 0;
      var boundarySides = new HashSet<string>();
      foreach (var cell in dryCells) {
        var x = cell % _map.Width;
        var y = cell / _map.Width;
        Visit(x - 1, y);
        Visit(x + 1, y);
        Visit(x, y - 1);
        Visit(x, y + 1);

        void Visit(int neighbourX, int neighbourY) {
          if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
            mapEdges++;
            boundarySides.Add(neighbourX < 0 ? "left" : neighbourX >= _map.Width ? "right"
                : neighbourY < 0 ? "bottom" : "top");
          } else if (_openWater[neighbourX + neighbourY * _map.Width]) {
            waterEdges++;
            if (_externalWater[neighbourX + neighbourY * _map.Width]) {
              externalWaterEdges++;
            }
          }
        }
      }
      return new ParentMeasurement(
          index, dryCells.Count, mapEdges > 0, boundarySides.Count,
          boundarySides.Contains("left") && boundarySides.Contains("right")
            || boundarySides.Contains("bottom") && boundarySides.Contains("top"),
          GetMaximumInteriorRadius(dryCells, _map.Width, _map.Height),
          waterEdges + mapEdges > 0 ? (double) externalWaterEdges / (waterEdges + mapEdges) : 0,
          cells);
    }

    WaterBodyMeasurement MeasureWaterBody(int index, IReadOnlyList<int> cells) {
      var landEdges = 0;
      var mapEdges = 0;
      foreach (var cell in cells) {
        var x = cell % _map.Width;
        var y = cell / _map.Width;
        Visit(x - 1, y);
        Visit(x + 1, y);
        Visit(x, y - 1);
        Visit(x, y + 1);

        void Visit(int neighbourX, int neighbourY) {
          if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
            mapEdges++;
          } else if (!_openWater[neighbourX + neighbourY * _map.Width]) {
            landEdges++;
          }
        }
      }
      return new WaterBodyMeasurement(
          index, landEdges + mapEdges > 0 ? (double) mapEdges / (landEdges + mapEdges) : 0);
    }

    IReadOnlyDictionary<int, double> MeasureDominantShorelineRatios(
        IReadOnlyList<List<int>> waterComponents, IReadOnlyList<ParentMeasurement> parents) {
      var parentByCell = Enumerable.Repeat(-1, _openWater.Length).ToArray();
      foreach (var parent in parents) {
        foreach (var cell in parent.Cells) {
          if (!_openWater[cell]) {
            parentByCell[cell] = parent.Index;
          }
        }
      }
      var result = new Dictionary<int, double>();
      foreach (var waterComponent in waterComponents) {
        var contacts = new Dictionary<int, int>();
        foreach (var cell in waterComponent) {
          var x = cell % _map.Width;
          var y = cell / _map.Width;
          Visit(x - 1, y);
          Visit(x + 1, y);
          Visit(x, y - 1);
          Visit(x, y + 1);

          void Visit(int neighbourX, int neighbourY) {
            if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
              return;
            }
            var parent = parentByCell[neighbourX + neighbourY * _map.Width];
            if (parent >= 0) {
              contacts[parent] = contacts.GetValueOrDefault(parent) + 1;
            }
          }
        }
        var totalContacts = contacts.Values.Sum();
        foreach (var (parent, contactEdges) in contacts) {
          var ratio = totalContacts > 0 ? (double) contactEdges / totalContacts : 0;
          result[parent] = Math.Max(result.GetValueOrDefault(parent), ratio);
        }
      }
      return result;
    }

    bool TouchesBoundary(IReadOnlyList<int> component) {
      return component.Any(cell => {
        var x = cell % _map.Width;
        var y = cell / _map.Width;
        return x == 0 || x == _map.Width - 1 || y == 0 || y == _map.Height - 1;
      });
    }
  }

  /// <summary>Finds useful projected dry-land island areas in descending order.</summary>
  public IReadOnlyList<int> Analyze(DecodedWaterMap map) {
    return Analyze(map, new WaterFeatureDiagnostics().Analyze(map));
  }

  /// <summary>Finds islands while reusing hydrology features already decoded for water classification.</summary>
  public IReadOnlyList<int> Analyze(DecodedWaterMap map, WaterFeatureAnalysis waterFeatures) {
    return new IslandAnalyzer(map, waterFeatures).Analyze();
  }

  static int GetRequiredCoreRadius(int width, int height) {
    var characteristic = Math.Sqrt(checked(width * height));
    return Math.Clamp((int) Math.Round(Math.Log2(characteristic) - 2), 2, 5);
  }

  static int GetExternalWaterOutletScale(int width, int height) {
    // This water-topology parameter currently starts from the same curve as the independent settlement core scale.
    var characteristic = Math.Sqrt(checked(width * height));
    return Math.Clamp((int) Math.Round(Math.Log2(characteristic) - 2), 2, 5);
  }

  static bool[] FindExternalWater(bool[] openWater, int width, int height, int outletScale) {
    var clearance = Enumerable.Repeat(int.MaxValue, openWater.Length).ToArray();
    var pending = new Queue<int>();
    for (var cell = 0; cell < openWater.Length; cell++) {
      if (!openWater[cell]) {
        continue;
      }
      var x = cell % width;
      var y = cell / width;
      if (Neighbours(x, y).Any(neighbour => !openWater[neighbour])) {
        clearance[cell] = 1;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      var x = cell % width;
      var y = cell / width;
      foreach (var neighbour in Neighbours(x, y)) {
        if (openWater[neighbour] && clearance[neighbour] > clearance[cell] + 1) {
          clearance[neighbour] = clearance[cell] + 1;
          pending.Enqueue(neighbour);
        }
      }
    }

    var externalCore = new bool[openWater.Length];
    pending.Clear();
    for (var cell = 0; cell < openWater.Length; cell++) {
      var x = cell % width;
      var y = cell / width;
      if (openWater[cell] && clearance[cell] >= outletScale
          && (x == 0 || x == width - 1 || y == 0 || y == height - 1)) {
        externalCore[cell] = true;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      var x = cell % width;
      var y = cell / width;
      foreach (var neighbour in Neighbours(x, y)) {
        if (openWater[neighbour] && clearance[neighbour] >= outletScale && !externalCore[neighbour]) {
          externalCore[neighbour] = true;
          pending.Enqueue(neighbour);
        }
      }
    }

    var externalWater = externalCore.ToArray();
    var distanceFromCore = Enumerable.Repeat(int.MaxValue, openWater.Length).ToArray();
    pending.Clear();
    for (var cell = 0; cell < externalCore.Length; cell++) {
      if (externalCore[cell]) {
        distanceFromCore[cell] = 0;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      if (distanceFromCore[cell] >= outletScale - 1) {
        continue;
      }
      var x = cell % width;
      var y = cell / width;
      foreach (var neighbour in Neighbours(x, y)) {
        if (openWater[neighbour] && distanceFromCore[neighbour] > distanceFromCore[cell] + 1) {
          distanceFromCore[neighbour] = distanceFromCore[cell] + 1;
          externalWater[neighbour] = true;
          pending.Enqueue(neighbour);
        }
      }
    }
    return externalWater;

    IEnumerable<int> Neighbours(int x, int y) {
      if (x > 0) {
        yield return x - 1 + y * width;
      }
      if (x < width - 1) {
        yield return x + 1 + y * width;
      }
      if (y > 0) {
        yield return x + (y - 1) * width;
      }
      if (y < height - 1) {
        yield return x + (y + 1) * width;
      }
    }
  }

  static bool[] KeepOpenBoundaryWater(
      bool[] gatedExternalWater, IReadOnlyList<List<int>> waterComponents,
      IReadOnlyList<WaterBodyMeasurement> waterBodies) {
    var result = new bool[gatedExternalWater.Length];
    var openBodyIndexes = waterBodies
        .Where(body => body.MapBoundaryRatio >= MinimumBoundaryWaterBodyRatio)
        .Select(body => body.Index)
        .ToHashSet();
    for (var componentIndex = 0; componentIndex < waterComponents.Count; componentIndex++) {
      if (!openBodyIndexes.Contains(componentIndex)) {
        continue;
      }
      foreach (var cell in waterComponents[componentIndex]) {
        result[cell] = gatedExternalWater[cell];
      }
    }
    return result;
  }

  static int GetMaximumInteriorRadius(IReadOnlyList<int> cells, int width, int height) {
    var cellSet = cells.ToHashSet();
    var distances = new Dictionary<int, int>();
    var pending = new Queue<int>();
    foreach (var cell in cells) {
      var x = cell % width;
      var y = cell / width;
      if (x == 0 || x == width - 1 || y == 0 || y == height - 1
          || GetNeighbours(cell).Any(neighbour => !cellSet.Contains(neighbour))) {
        distances[cell] = 1;
        pending.Enqueue(cell);
      }
    }
    while (pending.TryDequeue(out var cell)) {
      foreach (var neighbour in GetNeighbours(cell)) {
        if (cellSet.Contains(neighbour) && distances.TryAdd(neighbour, distances[cell] + 1)) {
          pending.Enqueue(neighbour);
        }
      }
    }
    return distances.Values.DefaultIfEmpty().Max();

    IEnumerable<int> GetNeighbours(int cell) {
      var x = cell % width;
      var y = cell / width;
      if (x > 0) {
        yield return cell - 1;
      }
      if (x < width - 1) {
        yield return cell + 1;
      }
      if (y > 0) {
        yield return cell - width;
      }
      if (y < height - 1) {
        yield return cell + width;
      }
    }
  }

  static IReadOnlyList<List<int>> FindComponents(bool[] mask, int width, int height) {
    var result = new List<List<int>>();
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

        void Visit(int neighbourX, int neighbourY) {
          if (neighbourX < 0 || neighbourX >= width || neighbourY < 0 || neighbourY >= height) {
            return;
          }
          var neighbour = neighbourX + neighbourY * width;
          if (mask[neighbour] && !visited[neighbour]) {
            visited[neighbour] = true;
            pending.Enqueue(neighbour);
          }
        }
      }
      result.Add(component);
    }
    return result;
  }

  static IReadOnlyList<List<int>> MergeAcrossRiverComponents(
      IReadOnlyList<List<int>> dryComponents, bool[] riverWater, bool[] externalWater,
      int width, int height, int requiredCoreRadius, int maximumNarrowWaterWidth) {
    var componentByCell = Enumerable.Repeat(-1, riverWater.Length).ToArray();
    for (var component = 0; component < dryComponents.Count; component++) {
      foreach (var cell in dryComponents[component]) {
        componentByCell[cell] = component;
      }
    }
    var parent = Enumerable.Range(0, dryComponents.Count).ToArray();
    var mergeableRiverWater = riverWater
        .Select((value, cell) => value && !externalWater[cell])
        .ToArray();
    var coreRadii = dryComponents
        .Select(component => GetMaximumInteriorRadius(component, width, height))
        .ToArray();
    var touchesBoundary = dryComponents
        .Select(component => component.Any(cell => {
          var x = cell % width;
          var y = cell / width;
          return x == 0 || x == width - 1 || y == 0 || y == height - 1;
        }))
        .ToArray();

    var owner = componentByCell.ToArray();
    var distance = owner.Select(value => value >= 0 ? 0 : int.MaxValue).ToArray();
    var pending = new Queue<int>();
    for (var cell = 0; cell < owner.Length; cell++) {
      if (owner[cell] >= 0) {
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
        if (owner[neighbour] >= 0 && owner[neighbour] != owner[cell]
            && distance[cell] + distance[neighbour] <= maximumNarrowWaterWidth
            && IsValidNarrowBank(owner[cell]) && IsValidNarrowBank(owner[neighbour])) {
          Union(owner[cell], owner[neighbour]);
        }
        if (!mergeableRiverWater[neighbour]
            || distance[cell] >= maximumNarrowWaterWidth || owner[neighbour] >= 0) {
          return;
        }
        owner[neighbour] = owner[cell];
        distance[neighbour] = distance[cell] + 1;
        pending.Enqueue(neighbour);
      }
    }

    foreach (var riverComponent in FindComponents(mergeableRiverWater, width, height)) {
      var bankComponents = new HashSet<int>();
      foreach (var cell in riverComponent) {
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
          var component = componentByCell[neighbourX + neighbourY * width];
          if (component >= 0 && IsUsefulBank(component)) {
            bankComponents.Add(component);
          }
        }
      }
      // A component with exactly two substantial banks is a river cut. More banks are ambiguous: they can also
      // describe a lake, sea, delta, or a river surrounding a true island.
      if (bankComponents.Count == 2) {
        var banks = bankComponents.ToArray();
        Union(banks[0], banks[1]);
      }
    }
    var groups = new Dictionary<int, List<int>>();
    for (var component = 0; component < dryComponents.Count; component++) {
      var root = Find(component);
      if (!groups.TryGetValue(root, out var cells)) {
        cells = [];
        groups[root] = cells;
      }
      cells.AddRange(dryComponents[component]);
    }
    return groups.Values.ToList();

    int Find(int component) {
      while (parent[component] != component) {
        parent[component] = parent[parent[component]];
        component = parent[component];
      }
      return component;
    }

    void Union(int first, int second) {
      var firstRoot = Find(first);
      var secondRoot = Find(second);
      if (firstRoot != secondRoot) {
        parent[secondRoot] = firstRoot;
      }
    }

    bool IsUsefulBank(int component) => coreRadii[component] >= requiredCoreRadius;

    bool IsValidNarrowBank(int component) => !touchesBoundary[component] || IsUsefulBank(component);
  }
}
