// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class MountainClassifier {
  /// <summary>Stable public-index key for non-overlapping projected mountain areas.</summary>
  public const string FeatureKey = "mountains";
  const double MaximumCliffRatio = 0.25;
  const double MaximumEnclosedDepressionRatio = 0.15;
  const double MinimumRadialDescentRatio = 0.75;
  const int MinimumProminence = 4;
  const int RayCount = 32;

  sealed record ContourLevel(IReadOnlyList<List<int>> Components, int[] OwnerByCell);

  sealed record MountainCandidate(IReadOnlyList<int> Cells);

  sealed record Peak(int Index, int Height, IReadOnlyList<int> Cells) {
    public int Representative => Cells[0];
  }

  sealed record ShapeMeasurement(double CliffRatio, int LargestEnclosedDepressionArea);

  /// <summary>Finds independent mountains and returns their non-overlapping projected areas.</summary>
  public IReadOnlyList<int> Analyze(DecodedWaterMap map) {
    var terrain = map.TerrainHeights;
    var peaks = FindPeaks(terrain, map.Width, map.Height);
    if (peaks.Count == 0) {
      return [];
    }

    var minimumHeight = terrain.Min();
    var maximumHeight = terrain.Max();
    var contours = Enumerable.Range(minimumHeight, maximumHeight - minimumHeight + 1)
        .ToDictionary(level => level, level => FindContourLevel(terrain, map.Width, map.Height, level));
    var minimumArea = Math.Max(25, (int) Math.Round(Math.Sqrt(terrain.Length)));
    var candidates = new List<MountainCandidate>();
    foreach (var peak in peaks) {
      var saddle = FindKeySaddle(peak, peaks, contours, minimumHeight);
      var prominence = peak.Height - saddle;
      if (prominence < MinimumProminence) {
        continue;
      }

      // The component immediately above the key saddle is the summit's complete lobe before it joins a higher peak.
      var footprintLevel = Math.Min(peak.Height, saddle + 1);
      var contour = contours[footprintLevel];
      var footprint = contour.Components[contour.OwnerByCell[peak.Representative]];
      if (footprint.Count < minimumArea) {
        continue;
      }
      var shape = MeasureShape(footprint, terrain, map.Width, map.Height);
      if (shape.CliffRatio > MaximumCliffRatio
          || shape.LargestEnclosedDepressionArea > footprint.Count * MaximumEnclosedDepressionRatio
          || MeasureRadialDescent(peak, prominence, terrain, map.Width, map.Height) < MinimumRadialDescentRatio) {
        continue;
      }
      candidates.Add(new MountainCandidate(footprint));
    }

    // Smaller saddle lobes own their cells first. A dominant mountain then receives the remaining shared base, so
    // neighbouring mountains never double-count projected coverage while minor peaks remain absorbed by their parent.
    var claimed = new bool[terrain.Length];
    var projectedAreas = new List<int>();
    foreach (var candidate in candidates.OrderBy(candidate => candidate.Cells.Count)) {
      var projectedCells = candidate.Cells.Where(cell => !claimed[cell]).ToList();
      if (projectedCells.Count < minimumArea) {
        continue;
      }
      foreach (var cell in projectedCells) {
        claimed[cell] = true;
      }
      projectedAreas.Add(projectedCells.Count);
    }
    return projectedAreas.OrderByDescending(area => area).ToList();
  }

  static int FindKeySaddle(
      Peak peak, IReadOnlyList<Peak> peaks, IReadOnlyDictionary<int, ContourLevel> contours,
      int minimumHeight) {
    var dominantPeaks = peaks.Where(other => other.Height > peak.Height
        || other.Height == peak.Height && other.Index < peak.Index).ToList();
    for (var level = peak.Height - 1; level >= minimumHeight; level--) {
      var contour = contours[level];
      var component = contour.OwnerByCell[peak.Representative];
      if (dominantPeaks.Any(other => contour.OwnerByCell[other.Representative] == component)) {
        return level;
      }
    }
    return minimumHeight;
  }

  static IReadOnlyList<Peak> FindPeaks(int[] terrain, int width, int height) {
    var peaks = new List<Peak>();
    var visited = new bool[terrain.Length];
    for (var start = 0; start < terrain.Length; start++) {
      if (visited[start]) {
        continue;
      }
      var level = terrain[start];
      var plateau = new List<int>();
      var pending = new Queue<int>();
      pending.Enqueue(start);
      visited[start] = true;
      var hasLowerNeighbour = false;
      var hasHigherNeighbour = false;
      while (pending.TryDequeue(out var cell)) {
        plateau.Add(cell);
        foreach (var neighbour in GetNeighbours(cell, width, height)) {
          if (terrain[neighbour] == level) {
            if (!visited[neighbour]) {
              visited[neighbour] = true;
              pending.Enqueue(neighbour);
            }
          } else if (terrain[neighbour] > level) {
            hasHigherNeighbour = true;
          } else {
            hasLowerNeighbour = true;
          }
        }
      }
      if (hasLowerNeighbour && !hasHigherNeighbour) {
        peaks.Add(new Peak(peaks.Count, level, plateau));
      }
    }
    return peaks;
  }

  static ContourLevel FindContourLevel(int[] terrain, int width, int height, int level) {
    var components = new List<List<int>>();
    var ownerByCell = Enumerable.Repeat(-1, terrain.Length).ToArray();
    for (var start = 0; start < terrain.Length; start++) {
      if (terrain[start] < level || ownerByCell[start] >= 0) {
        continue;
      }
      var componentIndex = components.Count;
      var component = new List<int>();
      var pending = new Queue<int>();
      pending.Enqueue(start);
      ownerByCell[start] = componentIndex;
      while (pending.TryDequeue(out var cell)) {
        component.Add(cell);
        foreach (var neighbour in GetNeighbours(cell, width, height)) {
          if (terrain[neighbour] >= level && ownerByCell[neighbour] < 0) {
            ownerByCell[neighbour] = componentIndex;
            pending.Enqueue(neighbour);
          }
        }
      }
      components.Add(component);
    }
    return new ContourLevel(components, ownerByCell);
  }

  static ShapeMeasurement MeasureShape(IReadOnlyList<int> cells, int[] terrain, int width, int height) {
    var mask = new bool[terrain.Length];
    foreach (var cell in cells) {
      mask[cell] = true;
    }

    var gradualEdges = 0;
    var cliffEdges = 0;
    foreach (var cell in cells) {
      var internalNeighbours = GetNeighbours(cell, width, height)
          .Where(neighbour => neighbour > cell && mask[neighbour]);
      foreach (var neighbour in internalNeighbours) {
        var difference = Math.Abs(terrain[cell] - terrain[neighbour]);
        if (difference == 1) {
          gradualEdges++;
        } else if (difference > 1) {
          cliffEdges++;
        }
      }
    }
    var measuredEdges = gradualEdges + cliffEdges;
    var cliffRatio = measuredEdges > 0 ? (double) cliffEdges / measuredEdges : 0;

    var largestDepression = 0;
    var visited = mask.ToArray();
    for (var start = 0; start < terrain.Length; start++) {
      if (visited[start]) {
        continue;
      }
      var area = 0;
      var touchesMapBoundary = false;
      var pending = new Queue<int>();
      pending.Enqueue(start);
      visited[start] = true;
      while (pending.TryDequeue(out var cell)) {
        area++;
        var x = cell % width;
        var y = cell / width;
        touchesMapBoundary |= x == 0 || x == width - 1 || y == 0 || y == height - 1;
        foreach (var neighbour in GetNeighbours(cell, width, height)) {
          if (!visited[neighbour]) {
            visited[neighbour] = true;
            pending.Enqueue(neighbour);
          }
        }
      }
      if (!touchesMapBoundary) {
        largestDepression = Math.Max(largestDepression, area);
      }
    }
    return new ShapeMeasurement(cliffRatio, largestDepression);
  }

  static double MeasureRadialDescent(Peak peak, int prominence, int[] terrain, int width, int height) {
    var peakX = peak.Cells.Average(cell => cell % width);
    var peakY = peak.Cells.Average(cell => cell / width);
    var requiredDrop = Math.Max(2, (int) Math.Ceiling(prominence / 2.0));
    var observedRays = 0;
    var descendingRays = 0;
    for (var ray = 0; ray < RayCount; ray++) {
      var angle = 2 * Math.PI * ray / RayCount;
      var deltaX = Math.Cos(angle);
      var deltaY = Math.Sin(angle);
      var samples = new List<int>();
      var lastCell = -1;
      for (var distance = 1; distance < Math.Max(width, height) * 2; distance++) {
        var x = (int) Math.Round(peakX + deltaX * distance);
        var y = (int) Math.Round(peakY + deltaY * distance);
        if (x < 0 || x >= width || y < 0 || y >= height) {
          break;
        }
        var cell = x + y * width;
        if (cell != lastCell) {
          samples.Add(terrain[cell]);
          lastCell = cell;
        }
      }
      if (samples.Count < 5) {
        continue;
      }
      observedRays++;
      if (peak.Height - samples.Min() >= requiredDrop) {
        descendingRays++;
      }
    }
    return observedRays > 0 ? (double) descendingRays / observedRays : 0;
  }

  static IEnumerable<int> GetNeighbours(int cell, int width, int height) {
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
