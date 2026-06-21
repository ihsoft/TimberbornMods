using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService;

public sealed class SoilOverridesService {
  int _nextContaminationOverrideId = 1;

  public readonly List<HashSet<Vector3Int>> AddedContaminationOverrides = [];
  public readonly List<int> RemovedContaminationOverrideIds = [];
  public readonly List<int> ClaimedContaminationOverrideIds = [];

  public int ActiveContaminationOverrideId { get; private set; } = -1;
  public HashSet<Vector3Int> ActiveContaminationOverrideTiles { get; private set; } = [];

  public int AddContaminationOverride(IEnumerable<Vector3Int> tiles) {
    var id = _nextContaminationOverrideId++;
    ActiveContaminationOverrideId = id;
    ActiveContaminationOverrideTiles = tiles.ToHashSet();
    AddedContaminationOverrides.Add(ActiveContaminationOverrideTiles);
    return id;
  }

  public void RemoveContaminationOverride(int overrideId) {
    RemovedContaminationOverrideIds.Add(overrideId);
    if (ActiveContaminationOverrideId == overrideId) {
      ActiveContaminationOverrideId = -1;
      ActiveContaminationOverrideTiles = [];
    }
  }

  public List<Vector3Int> ClaimContaminationOverrideIndex(int overrideId) {
    ClaimedContaminationOverrideIds.Add(overrideId);
    return [];
  }
}
