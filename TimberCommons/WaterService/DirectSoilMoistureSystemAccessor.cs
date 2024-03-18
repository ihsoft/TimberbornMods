// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.SceneLoading;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>Class that allows setting overrides to the soil moisture levels.</summary>
/// <remarks>
/// This code uses HarmonyX patches to access internal game's logic. Significant changes to it may break the mod.
/// </remarks>
public class DirectSoilMoistureSystemAccessor : IPostLoadableSingleton, ITickableSingleton {
  #region API
  // ReSharper disable UnusedMember.Global

  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case the maximum level will be used.
  /// </remarks>
  /// <param name="tiles">The tiles to apply the override to.</param>
  /// <param name="moistureLevel">The moister level.</param>
  /// <param name="moistureLevelForTextureFn">
  /// Optional function that provides moisture level that is only used to count the desert intensity on the terrain map.
  /// It won't affect moisture simulation. 
  /// </param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveMoistureOverride"/>
  public int AddMoistureOverride(IEnumerable<Vector2Int> tiles, float moistureLevel,
                                 Func<Vector2Int, float> moistureLevelForTextureFn = null) {
    var index = _nextMoistureOverrideId++;
    var moistureLevelDict = new Dictionary<int, float>();
    var desertLevelDict = new Dictionary<Vector2Int, float>();
    foreach (var tile in tiles) {
      moistureLevelDict.Add(_mapIndexService.CoordinatesToIndex(tile), moistureLevel);
      if (moistureLevelForTextureFn != null) {
        desertLevelDict.Add(tile, moistureLevelForTextureFn.Invoke(tile));
      }
    }
    _moistureLevelOverrides.Add(index, moistureLevelDict);
    _desertLevelOverrides.Add(index, desertLevelDict);
    _needMoistureOverridesUpdate = true;
    DebugEx.Fine("Added moisture override: id={0}, tiles={1}", index, moistureLevelDict.Count);
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
    _needMoistureOverridesUpdate = true;
    DebugEx.Fine("Removed moisture override: id={0}, tiles={1}", overrideId, tilesDict.Count);
  }

  /// <summary>Sets contamination blockers for a set of tiles.</summary>
  /// <remarks>The same tiles can be listed in multiple overrides.</remarks>
  /// <param name="tiles">The tiles to set the blocker at.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveContaminationOverride"/>
  public int AddContaminationOverride(IEnumerable<Vector2Int> tiles) {
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
  public void PostLoad() {
    MoistureLevelOverrides = new Dictionary<int, float>();
    TerrainTextureLevelsOverrides = new Dictionary<Vector2Int, float>();
    _eventBus.Register(this);
    _sceneLoader.SceneLoaded += OnSceneLoaded;
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
  internal static Dictionary<Vector2Int, float> TerrainTextureLevelsOverrides = new();

  readonly Dictionary<int, Dictionary<int, float>> _moistureLevelOverrides = new();
  readonly Dictionary<int, Dictionary<Vector2Int, float>> _desertLevelOverrides = new();
  int _nextMoistureOverrideId = 1;

  readonly Dictionary<int, HashSet<Vector2Int>> _contaminationOverrides = new();
  HashSet<Vector2Int> _contaminatedTilesCache = new();
  int _nextContaminationOverrideId = 1;

  MapIndexService _mapIndexService;
  SoilBarrierMap _soilBarrierMap;
  SoilMoistureMap _soilMoistureMap;
  BlockService _blockService;
  ITerrainService _terrainService;
  EventBus _eventBus;
  SceneLoader _sceneLoader;
  TerrainMaterialMap _terrainMaterialMap;

  /// <summary>Injects run-time dependencies.</summary>
  [Inject]
  public void InjectDependencies(MapIndexService mapIndexService, SoilBarrierMap soilBarrierMap,
                                 BlockService blockService, ITerrainService terrainService, EventBus eventBus,
                                 TerrainMaterialMap terrainMaterialMap, SoilMoistureMap soilMoistureMap,
                                 SceneLoader sceneLoader) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _blockService = blockService;
    _terrainService = terrainService;
    _eventBus = eventBus;
    _terrainMaterialMap = terrainMaterialMap;
    _soilMoistureMap = soilMoistureMap;
    _sceneLoader = sceneLoader;
  }

  /// <summary>Checks if there were changes and rebuilds moisture overrides caches.</summary>
  void UpdateMoistureOverrides() {
    if (!_needMoistureOverridesUpdate) {
      return;
    }
    _needMoistureOverridesUpdate = false;

    // Moisture levels. This affects the moisture simulation engine.
    MoistureLevelOverrides = new Dictionary<int, float>();
    foreach (var value in _moistureLevelOverrides.Values.SelectMany(item => item)) {
      var tile = value.Key;
      var level = value.Value;
      if (MoistureLevelOverrides.TryGetValue(tile, out var existingValue)) {
        MoistureLevelOverrides[tile] = Mathf.Max(level, existingValue);
      } else {
        MoistureLevelOverrides.Add(tile, level);
      }
    }
    DebugEx.Fine("Updated moisture level overrides: tiles={0}", MoistureLevelOverrides.Keys.Count);

    if (Features.OverrideDesertLevelsForWaterTowers) {
      UpdateTilesAppearance();
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
      var skipIt = _blockService
          .GetObjectsAt(new Vector3Int(tile.x, tile.y, _terrainService.CellHeight(tile)))
          .Any(IsContaminationBlocker);
      if (skipIt) {
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

  /// <summary>Requests the terrain appearance update to reflect the overriden desert levels.</summary>
  /// <seealso cref="TerrainTextureLevelsOverrides"/>
  void UpdateTilesAppearance() {
    // Moisture levels for the terrain texture. It only affects UI appearance.
    TerrainTextureLevelsOverrides = new Dictionary<Vector2Int, float>();
    foreach (var value in _desertLevelOverrides.Values.SelectMany(item => item)) {
      var tile = value.Key;
      var level = value.Value;
      var index = _mapIndexService.CoordinatesToIndex(tile);
      if (TerrainTextureLevelsOverrides.TryGetValue(tile, out var existingValue)) {
        TerrainTextureLevelsOverrides[tile] = Mathf.Max(level, existingValue);
      } else {
        TerrainTextureLevelsOverrides.Add(tile, level);
      }
    }
    foreach (var tile in TerrainTextureLevelsOverrides.Keys) {
      var index = _mapIndexService.CoordinatesToIndex(tile);
      _soilMoistureMap.UpdateDesertIntensity(tile, _soilMoistureMap.SoilMoisture(index));
    }
    DebugEx.Fine("Updated tiles appearance: tiles={0}", TerrainTextureLevelsOverrides.Keys.Count);
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

  #region Game load callbacks

  /// <summary>Refreshes terrain texture if when the game is loaded and there are active overrides.</summary>
  void OnSceneLoaded(object sender, EventArgs e) {
    _sceneLoader.SceneLoaded -= OnSceneLoaded;
    var needTextureUpdate = _needMoistureOverridesUpdate;
    Tick();

    // Refresh the texture on game load.
    if (needTextureUpdate && Features.OverrideDesertLevelsForWaterTowers) {
      _terrainMaterialMap.ProcessDesertTextureChanges();
      _terrainMaterialMap.ProcessDesertTextureChanges(); // Intentionally.
    }
  }

  #endregion
}

}
