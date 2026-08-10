// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable

using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class CanyonClassifier(DecodedWaterMap map) {
  /// <summary>Stable public-index key for measured canyon systems.</summary>
  public const string FeatureKey = "canyons";

  const double BankSearchDistance = 12;
  const double MaximumBottomWidth = 36;
  const double MaximumCyclicEdgeFraction = 0.4;
  const double MaximumEngineeredDirectionChangeFraction = 0.05;
  const double MaximumEngineeredWidthVariation = 0.12;
  const double MinimumBankHeight = 3;
  const double MinimumBankSlope = 0.25;
  const double MinimumExternalWaterBodyRatio = 0.4;
  const double MinimumExternalWaterCoverage = 0.8;
  const double MinimumExposedBankHeight = 0.5;
  const double MinimumLengthToWidthRatio = 4;
  const double MinimumMedianBankSlope = 0.75;
  const double MinimumMedianNormalAlignment = 0.7;
  const double MinimumSpatialExtentToLengthRatio = 0.45;
  const int DirectionCount = 16;
  const double RayStep = 0.25;

  sealed record Branch(
      IReadOnlyList<int> Cells, double DirectionChangeFraction);

  readonly record struct CrossSection(
      double FloorWidth, double LeftBankHeight, double RightBankHeight,
      double LeftSlope, double RightSlope, double DirectionX, double DirectionY) {
    public double BankHeight => Math.Min(LeftBankHeight, RightBankHeight);
  }

  sealed record NetworkTopology(int EndpointCount, int CycleRank, double CyclicEdgeFraction);

  readonly record struct RayMeasurement(
      bool IsValid, double FloorDistance, double BankHeight, double BankSlope);

  static readonly (double X, double Y)[] CrossSectionDirections = CreateCrossSectionDirections();
  static readonly (int X, int Y)[] NeighbourOffsets = [
      (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (-1, 1), (1, -1), (1, 1),
  ];

  readonly DecodedWaterMap _map = map;

  /// <summary>Finds connected canyon systems from terrain topology.</summary>
  public IReadOnlyList<CanyonClassification> Analyze() {
    var candidateFloor = new bool[checked(_map.Width * _map.Height)];
    var sections = new Dictionary<int, CrossSection>();
    for (var cell = 0; cell < candidateFloor.Length; cell++) {
      var section = MeasureBestCrossSection(cell);
      if (section is CrossSection measuredSection && IsCanyonSection(measuredSection)) {
        candidateFloor[cell] = true;
        sections[cell] = measuredSection;
      }
    }

    RemoveSmallComponents(candidateFloor, 8);
    var skeleton = Thin(candidateFloor);
    var branches = ExtractBranches(skeleton, candidateFloor)
        .Where(branch => IsConfinedBranch(branch, sections))
        .Select(branch => new Branch(branch, MeasureDirectionChangeFraction(branch)))
        .ToList();
    var waterBodyRatios = MeasureWaterBodyRatios();
    var minimumLength = Math.Clamp(Math.Round(Math.Sqrt(_map.Width * _map.Height) * 0.25), 24, 40);
    return MergeBranches(branches)
        .Select(cells => MeasureSystem(cells, branches, sections, waterBodyRatios, minimumLength))
        .Where(measurement => measurement is not null)
        .Select(measurement => measurement!)
        .OrderByDescending(measurement => measurement.Length)
        .ToList();
  }

  CanyonClassification? MeasureSystem(
      IReadOnlyList<int> cells, IReadOnlyList<Branch> allBranches,
      IReadOnlyDictionary<int, CrossSection> sections, double[] waterBodyRatios,
      double minimumLength) {
    var length = MeasureNetworkDiameter(cells);
    if (length < minimumLength) {
      return null;
    }
    var spanX = cells.Max(cell => cell % _map.Width) - cells.Min(cell => cell % _map.Width) + 1;
    var spanY = cells.Max(cell => cell / _map.Width) - cells.Min(cell => cell / _map.Width) + 1;
    var spatialExtent = Math.Sqrt(spanX * spanX + spanY * spanY);
    if (spatialExtent < length * MinimumSpatialExtentToLengthRatio) {
      // A path coiled inside a compact basin is not a long valley corridor even if its skeleton is lengthy.
      return null;
    }
    var systemSections = cells.Select(cell => sections[cell]).ToList();
    var averageWidth = systemSections.Average(section => section.FloorWidth);
    if (length < averageWidth * MinimumLengthToWidthRatio) {
      return null;
    }
    var topology = MeasureTopology(cells);
    if (topology.CyclicEdgeFraction > MaximumCyclicEdgeFraction) {
      return null;
    }

    var observedWaterCells = cells.Count(cell => _map.SurfaceDepths[cell] > 0);
    var observedWaterFraction = observedWaterCells / (double) cells.Count;
    var dominantWaterBodyRatio = cells.Where(cell => _map.SurfaceDepths[cell] > 0)
        .Select(cell => waterBodyRatios[cell]).DefaultIfEmpty(0).Max();
    if (observedWaterFraction >= MinimumExternalWaterCoverage
        && dominantWaterBodyRatio >= MinimumExternalWaterBodyRatio) {
      // A narrow spine through a map-scale lake is open water, not a submerged canyon floor.
      return null;
    }
    var medianExposedBankHeight = Percentile(cells
        .Select(cell => Math.Max(0, sections[cell].BankHeight - _map.SurfaceDepths[cell]))
        .Order().ToList(), 0.5);
    if (observedWaterFraction >= MinimumExternalWaterCoverage
        && medianExposedBankHeight < MinimumExposedBankHeight) {
      // Water reaching almost to both banks is a deep river channel rather than an exposed canyon floor.
      return null;
    }

    var widthVariation = CoefficientOfVariation(
        systemSections.Select(section => section.FloorWidth).ToList());
    var systemCells = cells.ToHashSet();
    var memberBranches = allBranches.Where(branch => branch.Cells.Any(systemCells.Contains)).ToList();
    var totalWeight = memberBranches.Sum(branch => branch.Cells.Count);
    var directionChangeFraction = totalWeight == 0 ? 0 : memberBranches.Sum(
        branch => branch.DirectionChangeFraction * branch.Cells.Count) / totalWeight;
    if (widthVariation <= MaximumEngineeredWidthVariation
        && directionChangeFraction <= MaximumEngineeredDirectionChangeFraction) {
      // Perfectly straight, constant-width trenches are engineered structures rather than natural canyon systems.
      return null;
    }

    var medianBankHeight = Percentile(
        systemSections.Select(section => section.BankHeight).Order().ToList(), 0.5);
    return new CanyonClassification(
        Math.Round(length, 1), Math.Round(averageWidth, 1), Math.Round(medianBankHeight, 1));
  }

  IReadOnlyList<IReadOnlyList<int>> MergeBranches(IReadOnlyList<Branch> branches) {
    var parents = Enumerable.Range(0, branches.Count).ToArray();
    var endpoints = branches.Select(branch => GetEndpoints(branch.Cells)).ToArray();
    for (var first = 0; first < branches.Count; first++) {
      for (var second = first + 1; second < branches.Count; second++) {
        var sharedEndpoint = endpoints[first].Intersect(endpoints[second]).FirstOrDefault(-1);
        if (sharedEndpoint >= 0) {
          Union(first, second);
        }
      }
    }
    return Enumerable.Range(0, branches.Count)
        .GroupBy(Find)
        .Select(group => (IReadOnlyList<int>) group
            .SelectMany(index => branches[index].Cells).Distinct().ToArray())
        .ToList();

    int Find(int node) {
      while (parents[node] != node) {
        parents[node] = parents[parents[node]];
        node = parents[node];
      }
      return node;
    }

    void Union(int first, int second) {
      var firstRoot = Find(first);
      var secondRoot = Find(second);
      if (firstRoot != secondRoot) {
        parents[secondRoot] = firstRoot;
      }
    }
  }

  IReadOnlyList<int> GetEndpoints(IReadOnlyList<int> cells) {
    var network = cells.ToHashSet();
    var endpoints = network.Where(cell => CountNetworkNeighbours(cell, network) <= 1).ToArray();
    if (endpoints.Length > 0) {
      return endpoints;
    }
    var firstSweep = FindFarthest(cells[0], network);
    var secondSweep = FindFarthest(firstSweep.Cell, network);
    return [firstSweep.Cell, secondSweep.Cell];
  }

  int CountNetworkNeighbours(int cell, IReadOnlySet<int> network) {
    var x = cell % _map.Width;
    var y = cell / _map.Width;
    return NeighbourOffsets.Count(offset => {
      var neighbourX = x + offset.X;
      var neighbourY = y + offset.Y;
      return neighbourX >= 0 && neighbourX < _map.Width && neighbourY >= 0 && neighbourY < _map.Height
          && !IsRedundantDiagonal(x, y, offset.X, offset.Y, network)
          && network.Contains(neighbourX + neighbourY * _map.Width);
    });
  }

  CrossSection? MeasureBestCrossSection(int cell) {
    CrossSection? best = null;
    foreach (var (directionX, directionY) in CrossSectionDirections) {
      var left = MeasureRay(cell, directionX, directionY);
      var right = MeasureRay(cell, -directionX, -directionY);
      if (!left.IsValid || !right.IsValid) {
        continue;
      }
      var section = new CrossSection(
          left.FloorDistance + right.FloorDistance,
          left.BankHeight, right.BankHeight, left.BankSlope, right.BankSlope,
          directionX, directionY);
      if (best is null) {
        best = section;
        continue;
      }
      var bestSection = best.Value;
      if (section.FloorWidth < bestSection.FloorWidth
          || Math.Abs(section.FloorWidth - bestSection.FloorWidth) < 0.01
          && section.BankHeight > bestSection.BankHeight) {
        best = section;
      }
    }
    return best;
  }

  RayMeasurement MeasureRay(int cell, double directionX, double directionY) {
    var startX = cell % _map.Width + 0.5;
    var startY = cell / _map.Width + 0.5;
    var floorHeight = _map.TerrainHeights[cell];
    var lastSample = -1;
    var firstRiseDistance = double.NaN;
    var maximumHeight = double.NegativeInfinity;
    var maximumHeightDistance = double.NaN;
    for (var distance = RayStep; distance <= MaximumBottomWidth + BankSearchDistance; distance += RayStep) {
      var x = (int) Math.Floor(startX + directionX * distance);
      var y = (int) Math.Floor(startY + directionY * distance);
      if (x < 0 || x >= _map.Width || y < 0 || y >= _map.Height) {
        return new RayMeasurement(false, 0, 0, 0);
      }
      var sample = x + y * _map.Width;
      // A straight ray is monotonic on both axes, so it can only repeat the cell visited immediately before it.
      if (sample == lastSample) {
        continue;
      }
      lastSample = sample;
      var height = _map.TerrainHeights[sample];
      if (double.IsNaN(firstRiseDistance)) {
        if (height < floorHeight) {
          return new RayMeasurement(false, 0, 0, 0);
        }
        if (height <= floorHeight) {
          continue;
        }
        firstRiseDistance = distance;
      }
      if (distance - firstRiseDistance > BankSearchDistance) {
        break;
      }
      if (height > maximumHeight) {
        maximumHeight = height;
        maximumHeightDistance = distance;
      }
    }
    if (double.IsNaN(firstRiseDistance) || double.IsNegativeInfinity(maximumHeight)) {
      return new RayMeasurement(false, 0, 0, 0);
    }
    var bankHeight = maximumHeight - floorHeight;
    var bankRun = Math.Max(0.5, maximumHeightDistance - firstRiseDistance + 0.5);
    return new RayMeasurement(true, firstRiseDistance, bankHeight, bankHeight / bankRun);
  }

  static (double X, double Y)[] CreateCrossSectionDirections() {
    var result = new (double X, double Y)[DirectionCount];
    for (var directionIndex = 0; directionIndex < DirectionCount; directionIndex++) {
      var angle = Math.PI * directionIndex / DirectionCount;
      result[directionIndex] = (Math.Cos(angle), Math.Sin(angle));
    }
    return result;
  }

  static bool IsCanyonSection(CrossSection section) {
    var widthLimit = Math.Min(MaximumBottomWidth, section.BankHeight * 2 + 2);
    return section.BankHeight >= MinimumBankHeight
        && section.LeftSlope >= MinimumBankSlope
        && section.RightSlope >= MinimumBankSlope
        && section.FloorWidth <= widthLimit;
  }

  bool IsConfinedBranch(
      IReadOnlyList<int> branch, IReadOnlyDictionary<int, CrossSection> sections) {
    var branchSections = branch.Select(cell => sections[cell]).ToList();
    var medianBankSlope = Percentile(branchSections
        .Select(section => Math.Min(section.LeftSlope, section.RightSlope)).Order().ToList(), 0.5);
    var medianNormalAlignment = Percentile(
        MeasureNormalAlignments(branch, sections).Order().ToList(), 0.5);
    return medianBankSlope >= MinimumMedianBankSlope
        && medianNormalAlignment >= MinimumMedianNormalAlignment;
  }

  IReadOnlyList<double> MeasureNormalAlignments(
      IReadOnlyList<int> branch, IReadOnlyDictionary<int, CrossSection> sections) {
    var result = new List<double>();
    for (var index = 0; index < branch.Count; index++) {
      var section = sections[branch[index]];
      var previous = branch[Math.Max(0, index - 1)];
      var next = branch[Math.Min(branch.Count - 1, index + 1)];
      var tangentX = next % _map.Width - previous % _map.Width;
      var tangentY = next / _map.Width - previous / _map.Width;
      var tangentLength = Math.Sqrt(tangentX * tangentX + tangentY * tangentY);
      if (tangentLength >= 0.01) {
        result.Add(Math.Abs(
            tangentX * section.DirectionY - tangentY * section.DirectionX) / tangentLength);
      }
    }
    return result;
  }

  double MeasureDirectionChangeFraction(IReadOnlyList<int> branch) {
    if (branch.Count < 3) {
      return 0;
    }
    var changes = 0;
    var previousX = Math.Sign(branch[1] % _map.Width - branch[0] % _map.Width);
    var previousY = Math.Sign(branch[1] / _map.Width - branch[0] / _map.Width);
    for (var index = 2; index < branch.Count; index++) {
      var directionX = Math.Sign(branch[index] % _map.Width - branch[index - 1] % _map.Width);
      var directionY = Math.Sign(branch[index] / _map.Width - branch[index - 1] / _map.Width);
      if (directionX != previousX || directionY != previousY) {
        changes++;
      }
      previousX = directionX;
      previousY = directionY;
    }
    return changes / (double) (branch.Count - 2);
  }

  double[] MeasureWaterBodyRatios() {
    var result = new double[checked(_map.Width * _map.Height)];
    var visited = new bool[result.Length];
    for (var start = 0; start < result.Length; start++) {
      if (visited[start] || _map.SurfaceDepths[start] <= 0) {
        continue;
      }
      var component = new List<int>();
      var pending = new Queue<int>();
      visited[start] = true;
      pending.Enqueue(start);
      while (pending.TryDequeue(out var cell)) {
        component.Add(cell);
        var x = cell % _map.Width;
        var y = cell / _map.Width;
        foreach (var (offsetX, offsetY) in NeighbourOffsets.Take(4)) {
          var neighbourX = x + offsetX;
          var neighbourY = y + offsetY;
          if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
            continue;
          }
          var neighbour = neighbourX + neighbourY * _map.Width;
          if (!visited[neighbour] && _map.SurfaceDepths[neighbour] > 0) {
            visited[neighbour] = true;
            pending.Enqueue(neighbour);
          }
        }
      }
      var ratio = component.Count / (double) result.Length;
      foreach (var cell in component) {
        result[cell] = ratio;
      }
    }
    return result;
  }

  double MeasureNetworkDiameter(IReadOnlyList<int> cells) {
    var network = cells.ToHashSet();
    var firstSweep = FindFarthest(cells[0], network);
    return FindFarthest(firstSweep.Cell, network).Distance + 1;
  }

  (int Cell, double Distance) FindFarthest(int start, IReadOnlySet<int> network) {
    var distances = new Dictionary<int, double> { [start] = 0 };
    var pending = new PriorityQueue<int, double>();
    pending.Enqueue(start, 0);
    var farthestCell = start;
    var farthestDistance = 0.0;
    while (pending.TryDequeue(out var cell, out var distance)) {
      if (distance > distances[cell]) {
        continue;
      }
      if (distance > farthestDistance) {
        farthestCell = cell;
        farthestDistance = distance;
      }
      var x = cell % _map.Width;
      var y = cell / _map.Width;
      foreach (var (offsetX, offsetY) in NeighbourOffsets) {
        var neighbourX = x + offsetX;
        var neighbourY = y + offsetY;
        if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height
            || IsRedundantDiagonal(x, y, offsetX, offsetY, network)) {
          continue;
        }
        var neighbour = neighbourX + neighbourY * _map.Width;
        if (!network.Contains(neighbour)) {
          continue;
        }
        var neighbourDistance = distance + (offsetX == 0 || offsetY == 0 ? 1 : Math.Sqrt(2));
        if (!distances.TryGetValue(neighbour, out var knownDistance) || neighbourDistance < knownDistance) {
          distances[neighbour] = neighbourDistance;
          pending.Enqueue(neighbour, neighbourDistance);
        }
      }
    }
    return (farthestCell, farthestDistance);
  }

  NetworkTopology MeasureTopology(IReadOnlyList<int> cells) {
    var network = cells.ToHashSet();
    var neighbours = network.ToDictionary(
        cell => cell,
        cell => GetNetworkNeighbours(cell, network).ToList());
    var edgeCount = neighbours.Values.Sum(value => value.Count) / 2;
    var discovery = new Dictionary<int, int>();
    var low = new Dictionary<int, int>();
    var bridgeCount = 0;
    var time = 0;
    foreach (var start in network.Where(cell => !discovery.ContainsKey(cell))) {
      FindBridges(start, -1);
    }
    var cyclicEdges = edgeCount - bridgeCount;
    return new NetworkTopology(
        neighbours.Values.Count(value => value.Count == 1),
        Math.Max(0, edgeCount - network.Count + 1),
        edgeCount == 0 ? 0 : cyclicEdges / (double) edgeCount);

    void FindBridges(int cell, int parent) {
      discovery[cell] = ++time;
      low[cell] = discovery[cell];
      foreach (var neighbour in neighbours[cell]) {
        if (neighbour == parent) {
          continue;
        }
        if (!discovery.ContainsKey(neighbour)) {
          FindBridges(neighbour, cell);
          low[cell] = Math.Min(low[cell], low[neighbour]);
          if (low[neighbour] > discovery[cell]) {
            bridgeCount++;
          }
        } else {
          low[cell] = Math.Min(low[cell], discovery[neighbour]);
        }
      }
    }
  }

  IEnumerable<int> GetNetworkNeighbours(int cell, IReadOnlySet<int> network) {
    var x = cell % _map.Width;
    var y = cell / _map.Width;
    foreach (var (offsetX, offsetY) in NeighbourOffsets) {
      var neighbourX = x + offsetX;
      var neighbourY = y + offsetY;
      if (neighbourX >= 0 && neighbourX < _map.Width && neighbourY >= 0 && neighbourY < _map.Height
          && !IsRedundantDiagonal(x, y, offsetX, offsetY, network)
          && network.Contains(neighbourX + neighbourY * _map.Width)) {
        yield return neighbourX + neighbourY * _map.Width;
      }
    }
  }

  bool IsRedundantDiagonal(
      int x, int y, int offsetX, int offsetY, IReadOnlySet<int> network) {
    return offsetX != 0 && offsetY != 0
        && (network.Contains(x + offsetX + y * _map.Width)
            || network.Contains(x + (y + offsetY) * _map.Width));
  }

  IReadOnlyList<List<int>> ExtractBranches(bool[] skeleton, bool[] candidateFloor) {
    var neighbours = new List<int>[skeleton.Length];
    for (var cell = 0; cell < skeleton.Length; cell++) {
      neighbours[cell] = skeleton[cell] ? GetGraphNeighbours(cell, skeleton, candidateFloor) : [];
    }
    var visitedEdges = new HashSet<long>();
    var branches = new List<List<int>>();
    for (var cell = 0; cell < skeleton.Length; cell++) {
      if (!skeleton[cell] || neighbours[cell].Count == 2) {
        continue;
      }
      foreach (var neighbour in neighbours[cell]) {
        WalkBranch(cell, neighbour);
      }
    }
    for (var cell = 0; cell < skeleton.Length; cell++) {
      foreach (var neighbour in neighbours[cell]) {
        WalkBranch(cell, neighbour);
      }
    }
    return branches;

    void WalkBranch(int start, int next) {
      if (!visitedEdges.Add(GetEdgeKey(start, next))) {
        return;
      }
      var branch = new List<int> { start };
      var previous = start;
      var current = next;
      while (true) {
        branch.Add(current);
        if (current == start || neighbours[current].Count != 2) {
          break;
        }
        var following = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];
        if (!visitedEdges.Add(GetEdgeKey(current, following))) {
          break;
        }
        previous = current;
        current = following;
      }
      branches.Add(branch);
    }
  }

  List<int> GetGraphNeighbours(int cell, bool[] mask, bool[] candidateFloor) {
    var result = new List<int>();
    var x = cell % _map.Width;
    var y = cell / _map.Width;
    foreach (var (offsetX, offsetY) in NeighbourOffsets) {
      var neighbourX = x + offsetX;
      var neighbourY = y + offsetY;
      if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
        continue;
      }
      if (offsetX != 0 && offsetY != 0) {
        var horizontal = x + offsetX + y * _map.Width;
        var vertical = x + (y + offsetY) * _map.Width;
        if (mask[horizontal] || mask[vertical]
            || !candidateFloor[horizontal] && !candidateFloor[vertical]) {
          // A diagonal skeleton step needs support from the original floor mask. Corner-touching basins must not
          // become one long canyon, while a thinned diagonal corridor still retains its wider source support.
          continue;
        }
      }
      var neighbour = neighbourX + neighbourY * _map.Width;
      if (mask[neighbour]) {
        result.Add(neighbour);
      }
    }
    return result;
  }

  static long GetEdgeKey(int first, int second) {
    return first < second
        ? ((long) first << 32) | (uint) second
        : ((long) second << 32) | (uint) first;
  }

  void RemoveSmallComponents(bool[] mask, int minimumSize) {
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
        var x = cell % _map.Width;
        var y = cell / _map.Width;
        foreach (var (offsetX, offsetY) in NeighbourOffsets) {
          var neighbourX = x + offsetX;
          var neighbourY = y + offsetY;
          if (neighbourX < 0 || neighbourX >= _map.Width || neighbourY < 0 || neighbourY >= _map.Height) {
            continue;
          }
          var neighbour = neighbourX + neighbourY * _map.Width;
          if (mask[neighbour] && !visited[neighbour]) {
            visited[neighbour] = true;
            pending.Enqueue(neighbour);
          }
        }
      }
      if (component.Count < minimumSize) {
        foreach (var cell in component) {
          mask[cell] = false;
        }
      }
    }
  }

  bool[] Thin(bool[] source) {
    var result = source.ToArray();
    var removed = new List<int>();
    var changed = true;
    while (changed) {
      changed = false;
      for (var pass = 0; pass < 2; pass++) {
        removed.Clear();
        for (var y = 1; y < _map.Height - 1; y++) {
          for (var x = 1; x < _map.Width - 1; x++) {
            var cell = x + y * _map.Width;
            if (!result[cell]) {
              continue;
            }
            var neighbours = GetClockwiseNeighbours(result, x, y);
            var count = neighbours.Count(value => value);
            var transitions = 0;
            for (var index = 0; index < neighbours.Length; index++) {
              if (!neighbours[index] && neighbours[(index + 1) % neighbours.Length]) {
                transitions++;
              }
            }
            var firstCondition = pass == 0
                ? !neighbours[0] || !neighbours[2] || !neighbours[4]
                : !neighbours[0] || !neighbours[2] || !neighbours[6];
            var secondCondition = pass == 0
                ? !neighbours[2] || !neighbours[4] || !neighbours[6]
                : !neighbours[0] || !neighbours[4] || !neighbours[6];
            if (count is >= 2 and <= 6 && transitions == 1 && firstCondition && secondCondition) {
              removed.Add(cell);
            }
          }
        }
        foreach (var cell in removed) {
          result[cell] = false;
        }
        changed |= removed.Count > 0;
      }
    }
    return result;
  }

  bool[] GetClockwiseNeighbours(bool[] mask, int x, int y) {
    return [
        mask[x + (y + 1) * _map.Width], mask[x + 1 + (y + 1) * _map.Width],
        mask[x + 1 + y * _map.Width], mask[x + 1 + (y - 1) * _map.Width],
        mask[x + (y - 1) * _map.Width], mask[x - 1 + (y - 1) * _map.Width],
        mask[x - 1 + y * _map.Width], mask[x - 1 + (y + 1) * _map.Width],
    ];
  }

  static double CoefficientOfVariation(IReadOnlyList<double> values) {
    var average = values.Average();
    if (average < 0.01) {
      return 0;
    }
    var variance = values.Sum(value => (value - average) * (value - average)) / values.Count;
    return Math.Sqrt(variance) / average;
  }

  static double Percentile(IReadOnlyList<double> sorted, double percentile) {
    if (sorted.Count == 0) {
      return 0;
    }
    var position = percentile * (sorted.Count - 1);
    var lower = (int) Math.Floor(position);
    var upper = (int) Math.Ceiling(position);
    return lower == upper
        ? sorted[lower]
        : sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
  }
}
