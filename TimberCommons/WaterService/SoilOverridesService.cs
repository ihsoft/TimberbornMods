// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberCommons.Settings;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.UILayoutSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService;

/// <summary>Class that allows setting overrides to the soil moisture levels.</summary>
/// <remarks>
/// This code uses HarmonyX patches to access internal game's logic. Significant changes to it may break the mod.
/// </remarks>
public class SoilOverridesService : ILoadableSingleton, ITickableSingleton {

  #region API
  // ReSharper disable UnusedMember.Global

  /// <summary>True if the game is loaded and started.</summary>
  public bool GameLoaded { get; private set; }

  /// <summary>Definition of the moisture override.</summary>
  public readonly record struct MoistureOverride(Vector3Int Coordinates, float MoistureLevel, float DesertLevel);

  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case the maximum level will be used.
  /// </remarks>
  /// <param name="moistureOverrides">The tiles mosuture ovveride onfo.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveMoistureOverride"/>
  public int AddMoistureOverride(IEnumerable<MoistureOverride> moistureOverrides) {
    var index = _nextMoistureOverrideId++;
    var moistureOverridesCopy = moistureOverrides.ToList();
    _moistureLevelOverrides.Add(index, moistureOverridesCopy);
    _needMoistureOverridesUpdate = true;
    DebugEx.Fine("Added moisture override: id={0}, overrides={1}", index, moistureOverridesCopy.Count);
    return index;
  }


  /// <summary>Removes the moisture override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddMoistureOverride"/>
  public void RemoveMoistureOverride(int overrideId) {
    if (!_moistureLevelOverrides.TryGetValue(overrideId, out var moistureOverrides)) {
      DebugEx.Warning("Trying to remove unknown moisture override ID: {0}", overrideId);
      return;
    }
    _moistureLevelOverrides.Remove(overrideId);
    _needMoistureOverridesUpdate = true;
    DebugEx.Fine("Removed moisture override: id={0}, tiles={1}", overrideId, moistureOverrides.Count);
  }

  /// <summary>Checks if there is a contamination barrier at the given coordinates.</summary>
  public bool IsContaminationBarrierAt(Vector3Int coordinates) {
    var barrier = _blockService.GetBottomObjectComponentAt<SoilBarrierSpec>(coordinates);
    return barrier && barrier.BlockContamination && barrier.GetComponentFast<BlockObject>().IsFinished;
  }

  /// <summary>Checks if there is a full moisture barrier at the given coordinates.</summary>
  public bool IsFullMoistureBarrierAt(Vector3Int coordinates) {
    var barrier = _blockService.GetBottomObjectComponentAt<SoilBarrierSpec>(coordinates);
    return barrier && barrier.BlockFullMoisture && barrier.GetComponentFast<BlockObject>().IsFinished;
  }

  /// <summary>Sets contamination blockers for a set of tiles.</summary>
  /// <remarks>The same tiles can be listed in multiple overrides.</remarks>
  /// <param name="tiles">The tiles to set the blocker at.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveContaminationOverride"/>
  public int AddContaminationOverride(IEnumerable<Vector3Int> tiles) {
    var tilesSet = tiles.ToHashSet();
    var index = _nextContaminationOverrideId++;
    _contaminationOverrides.Add(index, tilesSet);
    _needContaminationOverridesUpdate = true;
    DebugEx.Fine("Added contamination override: id={0}, tiles={1}", index, tilesSet.Count);
    return index;
  }

  /// <summary>Removes the contamination blockers override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddContaminationOverride"/>
  public void RemoveContaminationOverride(int overrideId) {
    if (!_contaminationOverrides.TryGetValue(overrideId, out var barriers)) {
      DebugEx.Warning("Trying to remove unknown contamination override ID: {0}", overrideId);
      return;
    }
    _contaminationOverrides.Remove(overrideId);
    _needContaminationOverridesUpdate = true;
    DebugEx.Fine("Removed contamination override: id={0}, tiles={1}", overrideId, barriers.Count);
  }

  // ReSharper restore UnusedMember.Global
  #endregion

  #region IPostLoadableSingleton implementation

  /// <summary>Sets up the moisture override logic.</summary>
  public void Load() {
    _eventBus.Register(this);
  }

  #endregion

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    UpdateMoistureOverrides();
    UpdateContaminationOverrides();
  }

  #endregion

  #region Implementation

  /// <summary>Map of the overriden moisture levels.</summary>
  internal static Dictionary<int, float> MoistureLevelOverrides = new();

  /// <summary>Map of the overriden desert levels.</summary>
  internal static Dictionary<Vector3Int, float> TerrainTextureLevelsOverrides = new();

  readonly Dictionary<int, List<MoistureOverride>> _moistureLevelOverrides = [];  
  int _nextMoistureOverrideId = 1;

  readonly Dictionary<int, HashSet<Vector3Int>> _contaminationOverrides = new();
  HashSet<Vector3Int> _contaminatedTilesCache = [];
  int _nextContaminationOverrideId = 1;

  readonly MapIndexService _mapIndexService;
  readonly SoilBarrierMap _soilBarrierMap;
  readonly SoilMoistureMap _soilMoistureMap;
  readonly BlockService _blockService;
  readonly EventBus _eventBus;
  readonly IThreadSafeColumnTerrainMap _columnTerrainMap;

  SoilOverridesService(MapIndexService mapIndexService, SoilBarrierMap soilBarrierMap,
                                   BlockService blockService, EventBus eventBus, SoilMoistureMap soilMoistureMap,
                                   IThreadSafeColumnTerrainMap columnTerrainMap) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _blockService = blockService;
    _eventBus = eventBus;
    _soilMoistureMap = soilMoistureMap;
    _columnTerrainMap = columnTerrainMap;

    MoistureLevelOverrides = new Dictionary<int, float>();
    TerrainTextureLevelsOverrides = new Dictionary<Vector3Int, float>();
  }

  /// <summary>Checks if there were changes and rebuilds moisture overrides caches.</summary>
  void UpdateMoistureOverrides() {
    if (!_needMoistureOverridesUpdate) {
      return;
    }
    _needMoistureOverridesUpdate = false;

    // Moisture levels. This affects the moisture simulation engine.
    MoistureLevelOverrides = new Dictionary<int, float>();
    TerrainTextureLevelsOverrides = new Dictionary<Vector3Int, float>();
    foreach (var moistureOverride in _moistureLevelOverrides.Values.SelectMany(item => item)) {
      var coordinates = moistureOverride.Coordinates;
      var index2D = _mapIndexService.CellToIndex(coordinates.XY());
      var hasColumn = _columnTerrainMap.TryGetIndexAtCeiling(index2D, coordinates.z, out var index3D);
      if (!hasColumn) {
        DebugEx.Error("Invalid tile coordinates in override: {0}", moistureOverride);
        continue;
      }

      var overrideMoistureLevel = moistureOverride.MoistureLevel;
      if (MoistureLevelOverrides.TryGetValue(index3D, out var existingMoistureLevel)) {
        MoistureLevelOverrides[index3D] = Mathf.Max(overrideMoistureLevel, existingMoistureLevel);
      } else {
        MoistureLevelOverrides.Add(index3D, overrideMoistureLevel);
      }

      var overrideDesertLevel = moistureOverride.DesertLevel;
      if (TerrainTextureLevelsOverrides.TryGetValue(coordinates, out var existingDesertLevel)) {
        TerrainTextureLevelsOverrides[coordinates] = Mathf.Max(overrideDesertLevel, existingDesertLevel);
      } else {
        TerrainTextureLevelsOverrides.Add(coordinates, overrideDesertLevel);
      }
    }
    DebugEx.Fine("Updated moisture overrides: nowTiles={0}", MoistureLevelOverrides.Keys.Count);
    if (!IrrigationSystemSettings.OverrideDesertLevelsForWaterTowers) {
      return;
    }

    // Update desert terrain texture. It only affects UI appearance.
    foreach (var coordinates in TerrainTextureLevelsOverrides.Keys) {
      var index2D = _mapIndexService.CellToIndex(coordinates.XY());
      if (!_columnTerrainMap.TryGetIndexAtCeiling(index2D, coordinates.z, out var index3D)) {
        throw new Exception("Invalid tile coordinates in override: " + coordinates);  // Unexpected!
      }
      _soilMoistureMap.UpdateDesertIntensity(coordinates, _soilMoistureMap.SoilMoisture(index3D));
    }
  }
  bool _needMoistureOverridesUpdate;

  /// <summary>Checks if there were changes and rebuilds contamination overrides cache.</summary>
  void UpdateContaminationOverrides() {
    if (!_needContaminationOverridesUpdate) {
      return;
    }
    _needContaminationOverridesUpdate = false;

    var newOverrides = _contaminationOverrides.SelectMany(item => item.Value).ToHashSet();
    foreach (var tile in newOverrides) {
      _soilBarrierMap.AddContaminationBarrierAt(tile);
    }
    var clearOverrides = _contaminatedTilesCache.Where(t => !newOverrides.Contains(t));
    foreach (var tile in clearOverrides) {
      if (IsContaminationBarrierAt(tile)) {
        DebugEx.Fine("Don't affect contamination barrier at: {0}", tile);
        continue;
      }
      _soilBarrierMap.RemoveContaminationBarrierAt(tile);
    }
    DebugEx.Fine("Update contamination overrides: oldActive={0}, newActive={1}",
                 _contaminatedTilesCache.Count, newOverrides.Count);
    _contaminatedTilesCache = newOverrides;
  }
  bool _needContaminationOverridesUpdate;

  /// <summary>Reacts on contamination blockers removal.</summary>
  /// <remarks>If there is an overriden blocker for the tile, then the barrier is restored.</remarks>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var barrierSpec = e.Entity.GetComponentFast<SoilBarrierSpec>();
    if (!barrierSpec || !barrierSpec.BlockContamination) {
      return;
    }
    var blockObject = e.Entity.GetComponentFast<BlockObject>();
    if (!blockObject.IsFinished || !_contaminatedTilesCache.Contains(blockObject.Coordinates)) {
      return;
    }
    DebugEx.Fine("Restore contamination barrier at: {0}", blockObject.Coordinates);
    _soilBarrierMap.AddContaminationBarrierAt(blockObject.Coordinates);
  }

  #endregion

  #region Game load callbacks

  // FIXME: doesn't work in 0.7.6.1
  /// <summary>Refreshes terrain texture if when the game is loaded and there are active overrides.</summary>
  // void OnSceneLoaded(object sender, EventArgs e) {
  //   _sceneLoader.SceneLoaded -= OnSceneLoaded;
  //   var needTextureUpdate = _needMoistureOverridesUpdate;
  //   Tick();
  //
  //   // Refresh the texture on game load.
  //   if (needTextureUpdate && IrrigationSystemSettings.OverrideDesertLevelsForWaterTowers) {
  //     // FIXME: Consider calling Tick() instead. It's public.
  //     _terrainMaterialMap.ProcessDesertTextureChanges();
  //     _terrainMaterialMap.ProcessDesertTextureChanges(); // Intentionally.
  //   }
  // }

  [OnEvent]
  public void OnNewGameInitialized(ShowPrimaryUIEvent newGameInitializedEvent) {
    GameLoaded = true;
  }

  #endregion
}