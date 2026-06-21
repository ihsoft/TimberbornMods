using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService;

public sealed class SoilOverridesService {
  int _nextContaminationOverrideId = 1;
  int _nextMoistureOverrideId = 1;

  public readonly List<HashSet<Vector3Int>> AddedContaminationOverrides = [];
  public readonly List<int> RemovedContaminationOverrideIds = [];
  public readonly List<int> ClaimedContaminationOverrideIds = [];
  public readonly List<List<MoistureOverride>> AddedMoistureOverrides = [];
  public readonly List<int> RemovedMoistureOverrideIds = [];
  public readonly List<int> ClaimedMoistureOverrideIds = [];
  public bool GameLoaded { get; set; } = true;

  public int ActiveContaminationOverrideId { get; private set; } = -1;
  public HashSet<Vector3Int> ActiveContaminationOverrideTiles { get; private set; } = [];
  public int ActiveMoistureOverrideId { get; private set; } = -1;
  public List<MoistureOverride> ActiveMoistureOverrides { get; private set; } = [];

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

  public int AddMoistureOverride(IEnumerable<MoistureOverride> moistureOverrides) {
    var id = _nextMoistureOverrideId++;
    ActiveMoistureOverrideId = id;
    ActiveMoistureOverrides = moistureOverrides.ToList();
    AddedMoistureOverrides.Add(ActiveMoistureOverrides);
    return id;
  }

  public void RemoveMoistureOverride(int overrideId) {
    RemovedMoistureOverrideIds.Add(overrideId);
    if (ActiveMoistureOverrideId == overrideId) {
      ActiveMoistureOverrideId = -1;
      ActiveMoistureOverrides = [];
    }
  }

  public void ClaimMoistureOverrideIndex(int overrideId) {
    ClaimedMoistureOverrideIds.Add(overrideId);
  }

  public bool IsFullMoistureBarrierAt(Vector3Int coordinates) {
    return false;
  }
}

public readonly record struct MoistureOverride(Vector3Int Coordinates, float MoistureLevel, float DesertLevel);
