// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.TerrainSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>Class that allows setting overrides to the soil moisture levels.</summary>
/// <remarks>
/// This code uses HarmonyX patches to access internal game's logic. Significant changes to it may break the mod.
/// </remarks>
public class DirectSoilMoistureSystemAccessor : IPostLoadableSingleton {
  #region API
  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case the maximum level will be used.
  /// </remarks>
  /// <param name="tiles">The tiles to apply the override to.</param>
  /// <param name="moisture">The moister level.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveMoistureOverride"/>
  public int AddMoistureOverride(IEnumerable<Vector2Int> tiles, float moisture) {
    var index = _nextMoistureOverrideId++;
    var tilesDict = tiles.Select(c => _mapIndexService.CoordinatesToIndex(c))
        .ToDictionary(k => k, _ => moisture);
    _moistureOverrides.Add(index,tilesDict);
    var oldCacheSize = MoistureOverrides?.Count;
    UpdateMoistureMap();
    DebugEx.Fine("Added moisture override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 index, tilesDict.Count, oldCacheSize, MoistureOverrides?.Count);
    return index;
  }

  /// <summary>Removes the moisture override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddMoistureOverride"/>
  public void RemoveMoistureOverride(int overrideId) {
    if (!_moistureOverrides.TryGetValue(overrideId, out var tilesDict)) {
      return;
    }
    _moistureOverrides.Remove(overrideId);
    var oldCacheSize = MoistureOverrides?.Count;
    UpdateMoistureMap();
    DebugEx.Fine("Removed moisture override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 overrideId, tilesDict.Count, oldCacheSize, MoistureOverrides?.Count);
  }

  /// <summary>Sets contamination blockers for a set of tiles.</summary>
  /// <remarks>The same tiles can be listed in multiple overrides.</remarks>
  /// <param name="tiles">The tiles to set the blocker at.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveContaminationOverride"/>
  public int AddContaminationOverride(IEnumerable<Vector2Int> tiles) {
    var tilesSet = tiles.ToHashSet();
    var oldCacheSize = _contaminatedTilesCache.Count;
    var index = _nextContaminationOverrideId++;
    _contaminationOverrides.Add(index, tilesSet);
    _contaminatedTilesCache = _contaminationOverrides.SelectMany(item => item.Value).ToHashSet();
    foreach (var barrierTile in tilesSet) {
      _soilBarrierMap.AddContaminationBarrierAt(barrierTile);
    }
    DebugEx.Fine("Added contamination override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 index, tilesSet.Count, oldCacheSize, _contaminatedTilesCache.Count);
    return index;
  }

  /// <summary>Removes the contamination blockers override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddContaminationOverride"/>
  public void RemoveContaminationOverride(int overrideId) {
    if (!_contaminationOverrides.TryGetValue(overrideId, out var barriers)) {
      return;
    }
    var oldTilesCache = _contaminatedTilesCache;
    _contaminationOverrides.Remove(overrideId);
    _contaminatedTilesCache = _contaminationOverrides.SelectMany(item => item.Value).ToHashSet();
    var barriersToRemove = oldTilesCache.Where(x => !_contaminatedTilesCache.Contains(x));
    foreach (var barrier in barriersToRemove) {
      var skipIt = _blockService
          .GetObjectsAt(new Vector3Int(barrier.x, barrier.y, _terrainService.CellHeight(barrier)))
          .Any(IsContaminationBlocker);
      if (skipIt) {
        DebugEx.Fine("Don't affect contamination barrier at: {0}", barrier);
        continue;
      }
      _soilBarrierMap.RemoveContaminationBarrierAt(barrier);
    }
    DebugEx.Fine("Removed contamination override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 overrideId, barriers.Count, oldTilesCache.Count, _contaminatedTilesCache.Count);
  }
  #endregion

  #region IPostLoadableSingleton implementation
  /// <summary>Sets up the moisture override logic.</summary>
  public void PostLoad() {
    MoistureOverrides = null;
    _eventBus.Register(this);
  }
  #endregion

  #region Implementation

  /// <summary>Map of the overriden levels.</summary>
  /// <remarks>It will be accessed from the threads, so don't modify the dict once assigned.</remarks>
  internal static Dictionary<int, float> MoistureOverrides;
  readonly Dictionary<int, Dictionary<int, float>> _moistureOverrides = new();
  int _nextMoistureOverrideId = 1;

  readonly Dictionary<int, HashSet<Vector2Int>> _contaminationOverrides = new();
  HashSet<Vector2Int> _contaminatedTilesCache = new();
  int _nextContaminationOverrideId = 1;

  MapIndexService _mapIndexService;
  SoilBarrierMap _soilBarrierMap;
  BlockService _blockService;
  ITerrainService _terrainService;
  EventBus _eventBus;

  /// <summary>Injects run-time dependencies.</summary>
  [Inject]
  public void InjectDependencies(MapIndexService mapIndexService, SoilBarrierMap soilBarrierMap,
                                 BlockService blockService, ITerrainService terrainService, EventBus eventBus) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _blockService = blockService;
    _terrainService = terrainService;
    _eventBus = eventBus;
  }


  /// <summary>Recalculates moisture override cache.</summary>
  void UpdateMoistureMap() {
    var overridesCache = new Dictionary<int, float>();
    foreach (var value in _moistureOverrides.Values.SelectMany(item => item)) {
      if (overridesCache.TryGetValue(value.Key, out var existingValue)) {
        overridesCache[value.Key] = Mathf.Max(value.Value, existingValue);
      } else {
        overridesCache.Add(value.Key, value.Value);
      }
    }
    // Must be thread-safe.
    if (overridesCache.Count == 0) {
      overridesCache = null;
    }
    Interlocked.Exchange(ref MoistureOverrides, overridesCache);
  }

  /// <summary>Reacts on contamination blockers removal.</summary>
  /// <remarks>If there are overriden blocker for the tile, then the barrier is restored.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var constructible = e.Entity.GetComponentFast<Constructible>();
    if (constructible == null || !constructible.IsFinished) {
      return; // Ignore preview objects.
    }
    var tile = constructible.GetComponentFast<BlockObject>().Coordinates.XY();
    if (_contaminatedTilesCache.Contains(tile) && IsContaminationBlocker(constructible)) {
      DebugEx.Fine("Restore contamination barrier at: {0}", tile);
      _soilBarrierMap.AddContaminationBarrierAt(tile);
    }
  }

  /// <summary>Tells if the building blocks soil contamination.</summary>
  static bool IsContaminationBlocker(BaseComponent component) {
    var barrier = component.GetComponentFast<SoilBarrier>();
    return barrier != null && barrier._blockContamination;
  }
  #endregion

}

}
