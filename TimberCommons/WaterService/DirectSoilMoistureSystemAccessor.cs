// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
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
  // ReSharper disable UnusedMember.Global

  /// <summary>The tile looks shiny green.</summary>
  public const float DesertLevelWellMoisturized = 0.2f;

  /// <summary>The tile looks mostly green, but has some brown spots.</summary>
  public const float DesertLevelMoisturized = 0.34f;

  /// <summary>The tile looks "half-green"/"half-brown".</summary>
  public const float DesertLevelNotEnoughMoisturized = 0.48f;

  /// <summary>The tile looks mostly brow, but has some green spots.</summary>
  public const float DesertLevelNearlyDry = 0.56f;
  
  /// <summary>The tile looks almost completely brown, but it has a few green spots.</summary>
  public const float DesertLevelAlmostDry = 0.68f;

  /// <summary>The tile looks completely brown.</summary>
  public const float DesertLevelBarren = 1.0f;

  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case the maximum level will be used.
  /// </remarks>
  /// <param name="tiles">The tiles to apply the override to.</param>
  /// <param name="moistureLevel">The moister level.</param>
  /// <param name="desertLevelFn">
  /// Optional function to make a "desert degree" of the tile on the map. The returned value must be in range [0; 1],
  /// where 0 is completely green tile and 1 is completely brown. It only affects UI. If not provided, then the stock
  /// logic will calculate the value based on the actual moisture levels from teh simulation engine. 
  /// </param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveMoistureOverride"/>
  /// <seealso cref="DesertLevelWellMoisturized"/>
  /// <seealso cref="DesertLevelMoisturized"/>
  /// <seealso cref="DesertLevelNotEnoughMoisturized"/>
  /// <seealso cref="DesertLevelNearlyDry"/>
  /// <seealso cref="DesertLevelAlmostDry"/>
  /// <seealso cref="DesertLevelBarren"/>
  public int AddMoistureOverride(IEnumerable<Vector2Int> tiles, float moistureLevel,
                                 Func<Vector2Int, float> desertLevelFn = null) {
    var index = _nextMoistureOverrideId++;
    var moistureLevelDict = new Dictionary<int, float>();
    var desertLevelDict = new Dictionary<Vector2Int, float>();
    foreach (var tile in tiles) {
      moistureLevelDict.Add(_mapIndexService.CoordinatesToIndex(tile), moistureLevel);
      if (desertLevelFn != null) {
        desertLevelDict.Add(tile, desertLevelFn.Invoke(tile));
      }
    }
    _moistureLevelOverrides.Add(index, moistureLevelDict);
    _desertLevelOverrides.Add(index, desertLevelDict);
    var oldCacheSize = MoistureLevelOverrides?.Count;
    UpdateMoistureMap();
    UpdateTilesAppearance(desertLevelDict.Keys);

    DebugEx.Fine("Added moisture override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 index, moistureLevelDict.Count, oldCacheSize, MoistureLevelOverrides?.Count);
    return index;
  }

  /// <summary>Removes the moisture override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddMoistureOverride"/>
  public void RemoveMoistureOverride(int overrideId) {
    if (!_desertLevelOverrides.TryGetValue(overrideId, out var tilesDict)) {
      return;
    }
    _moistureLevelOverrides.Remove(overrideId);
    _desertLevelOverrides.Remove(overrideId);
    var oldCacheSize = MoistureLevelOverrides?.Count;
    UpdateMoistureMap();
    UpdateTilesAppearance(tilesDict.Keys);

    DebugEx.Fine("Removed moisture override: id={0}, tiles={1}. Cache size: {2} => {3}",
                 overrideId, tilesDict.Count, oldCacheSize, MoistureLevelOverrides?.Count);
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

  // ReSharper restore UnusedMember.Global
  #endregion

  #region IPostLoadableSingleton implementation
  /// <summary>Sets up the moisture override logic.</summary>
  public void PostLoad() {
    MoistureLevelOverrides = null;
    DesertLevelOverrides = null;
    _eventBus.Register(this);
  }

  #endregion

  #region Implementation

  /// <summary>Map of the overriden moisture levels.</summary>
  internal static Dictionary<int, float> MoistureLevelOverrides;

  /// <summary>Map of the overriden desert levels.</summary>
  internal static Dictionary<Vector2Int, float> DesertLevelOverrides;

  readonly Dictionary<int, Dictionary<int, float>> _moistureLevelOverrides = new();
  readonly Dictionary<int, Dictionary<Vector2Int, float>> _desertLevelOverrides = new();
  int _nextMoistureOverrideId = 1;

  readonly Dictionary<int, HashSet<Vector2Int>> _contaminationOverrides = new();
  HashSet<Vector2Int> _contaminatedTilesCache = new();
  int _nextContaminationOverrideId = 1;

  MapIndexService _mapIndexService;
  SoilBarrierMap _soilBarrierMap;
  BlockService _blockService;
  ITerrainService _terrainService;
  EventBus _eventBus;
  TerrainMaterialMap _terrainMaterialMap;

  /// <summary>Injects run-time dependencies.</summary>
  [Inject]
  public void InjectDependencies(MapIndexService mapIndexService, SoilBarrierMap soilBarrierMap,
                                 BlockService blockService, ITerrainService terrainService, EventBus eventBus,
                                 TerrainMaterialMap terrainMaterialMap) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _blockService = blockService;
    _terrainService = terrainService;
    _eventBus = eventBus;
    _terrainMaterialMap = terrainMaterialMap;
  }

  /// <summary>Recalculates moisture override cache.</summary>
  void UpdateMoistureMap() {
    // Moisture levels. This affects the moisture simulation engine.
    var moistureOverridesCache = new Dictionary<int, float>();
    foreach (var value in _moistureLevelOverrides.Values.SelectMany(item => item)) {
      if (moistureOverridesCache.TryGetValue(value.Key, out var existingValue)) {
        moistureOverridesCache[value.Key] = Mathf.Max(value.Value, existingValue);
      } else {
        moistureOverridesCache.Add(value.Key, value.Value);
      }
    }
    MoistureLevelOverrides = moistureOverridesCache.Count == 0 ? null : moistureOverridesCache;

    // Desert levels. It only affects UI appearance.
    if (!Features.OverrideDesertLevelsForWaterTowers) {
      return;
    }
    var desertOverridesCache = new Dictionary<Vector2Int, float>();
    foreach (var value in _desertLevelOverrides.Values.SelectMany(item => item)) {
      if (desertOverridesCache.TryGetValue(value.Key, out var existingValue)) {
        desertOverridesCache[value.Key] = Mathf.Min(value.Value, existingValue);
      } else {
        desertOverridesCache.Add(value.Key, value.Value);
      }
    }
    DesertLevelOverrides = desertOverridesCache.Count == 0 ? null : desertOverridesCache;
  }

  /// <summary>Requests the terrain appearance update to reflect the overriden desert levels.</summary>
  /// <seealso cref="DesertLevelOverrides"/>
  void UpdateTilesAppearance(IEnumerable<Vector2Int> tiles) {
    if (!Features.OverrideDesertLevelsForWaterTowers) {
      return;
    }
    foreach (var tile in tiles) {
      _terrainMaterialMap.SetDesertIntensity(tile, _terrainMaterialMap.GetDesertIntensity(tile));
    }
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
