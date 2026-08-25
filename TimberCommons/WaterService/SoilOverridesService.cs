// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.Utils;
using ProtoBuf;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using Timberborn.TerrainSystemRendering;
using Timberborn.TickSystem;
using Timberborn.UILayoutSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService;

/// <summary>Class that allows setting overrides to the soil moisture levels.</summary>
/// <remarks>
/// This code uses HarmonyX patches to access internal game's logic. Significant changes to it may break the mod.
/// </remarks>
public class SoilOverridesService : ILoadableSingleton, ITickableSingleton, IPostLoadableSingleton, ISaveableSingleton {

  #region API
  // ReSharper disable UnusedMember.Global

  /// <summary>True if the game is loaded and fully ready to start ticking.</summary>
  /// <remarks>This is the next step after "PostLoad".</remarks>
  public bool GameLoaded { get; private set; }

  /// <summary>Reclaims the loaded moisture override.</summary>
  /// <remarks>
  /// The components must recall their overrides and reclaim them on a load. Unclaimed overrides will be removed before
  /// the first state update.
  /// </remarks>
  public List<MoistureOverride> ClaimMoistureOverrideIndex(int overrideIndex) {
    if (!_loadedMoistureOverrideIndexes.Remove(overrideIndex)) {
      throw new Exception("Trying to claim unknown moisture override index: {0}" + overrideIndex);
    }
    DebugEx.Fine("Claimed moisture override: index={0}", overrideIndex);
    return _moistureLevelOverrides[overrideIndex];
  }

  /// <summary>Reclaims the loaded contamination blocker override.</summary>
  /// <remarks>
  /// The components must recall their overrides and reclaim them on a load. Unclaimed overrides will be removed before
  /// the first state update.
  /// </remarks>
  public List<Vector3Int> ClaimContaminationOverrideIndex(int overrideId) {
    if (!_loadedContaminationOverrideIndexes.Remove(overrideId)) {
      throw new Exception("Trying to claim unknown contamination blocker override index: {0}" + overrideId);
    }
    DebugEx.Fine("Claimed contamination blocker override: index={0}", overrideId);
    return _contaminationOverrides[overrideId].ToList();
  }

  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case, the maximum level will be used.
  /// </remarks>
  /// <param name="moistureOverrides">The tiles moisture override info.</param>
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
    if (!_moistureLevelOverrides.Remove(overrideId, out var moistureOverrides)) {
      DebugEx.Warning("Trying to remove unknown moisture override ID: {0}", overrideId);
      return;
    }
    _needMoistureOverridesUpdate = true;
    DebugEx.Fine("Removed moisture override: id={0}, tiles={1}", overrideId, moistureOverrides.Count);
  }

  /// <summary>Checks if there is a contamination barrier at the given coordinates.</summary>
  public bool IsContaminationBarrierAt(Vector3Int coordinates) {
    var blockObject = _blockService.GetBottomObjectComponentAt<BlockObject>(coordinates);
    if (!blockObject) {
      return false;
    }
    var barrier = blockObject.GetComponent<SoilBarrierSpec>();
    return blockObject.IsFinished && barrier != null && barrier.BlockContamination;
  }

  /// <summary>Checks if there is a full moisture barrier at the given coordinates.</summary>
  public bool IsFullMoistureBarrierAt(Vector3Int coordinates) {
    var blockObject = _blockService.GetBottomObjectComponentAt<BlockObject>(coordinates);
    if (!blockObject) {
      return false;
    }
    var barrier = blockObject.GetComponent<SoilBarrierSpec>();
    return blockObject.IsFinished && barrier != null && barrier.BlockFullMoisture;
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
    if (!_contaminationOverrides.Remove(overrideId, out var barriers)) {
      DebugEx.Warning("Trying to remove unknown contamination override ID: {0}", overrideId);
      return;
    }
    _needContaminationOverridesUpdate = true;
    DebugEx.Fine("Removed contamination override: id={0}, tiles={1}", overrideId, barriers.Count);
  }

  // ReSharper restore UnusedMember.Global
  #endregion

  #region Persistence implementation

  static readonly SingletonKey TreeCuttingAreaKey = new("SoilOverridesService");
  static readonly PropertyKey<string> OverridesStateKey = new("OverridesState");

  readonly List<int> _loadedMoistureOverrideIndexes = [];
  readonly List<int> _loadedContaminationOverrideIndexes = [];

  [ProtoContract]
  record MoistureOverrideProto {
    [ProtoMember(1)] public int OverrideId { get; init; }
    [ProtoMember(2)] public List<MoistureOverride> OverridesList { get; init; } = [];
  }

  [ProtoContract]
  record ContaminationOverrideProto {
    [ProtoMember(1)] public int OverrideId { get; init; }
    [ProtoMember(2)] public HashSet<Vector3Int> OverridesList { get; init; }
  }

  [ProtoContract]
  record SavedStateProto {
    [ProtoMember(1)] public List<MoistureOverrideProto> MoistureOverrides { get; init; } = [];
    [ProtoMember(2)] public List<ContaminationOverrideProto> ContaminationOverrides { get; init; } = [];
  }

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    var component = singletonSaver.GetSingleton(TreeCuttingAreaKey);
    var protoState = new SavedStateProto {
        MoistureOverrides = _moistureLevelOverrides.Select(x => new MoistureOverrideProto {
            OverrideId = x.Key,
            OverridesList = x.Value,
        }).ToList(),
        ContaminationOverrides = _contaminationOverrides.Select(x => new ContaminationOverrideProto {
            OverrideId = x.Key,
            OverridesList = x.Value,
        }).ToList(),
    };
    component.Set(OverridesStateKey, StringProtoSerializer.Serialize(protoState));
  }

  /// <inheritdoc/>
  public void Load() {
    _eventBus.Register(this);
    
    var singletonLoader = StaticBindings.DependencyContainer.GetInstance<ISingletonLoader>();
    if (!singletonLoader.TryGetSingleton(TreeCuttingAreaKey, out var objectLoader)) {
      return;
    }
    var serializedState = objectLoader.GetValueOrDefault(OverridesStateKey);
    if (string.IsNullOrEmpty(serializedState)) {
      return;
    }
    var protoState = StringProtoSerializer.Deserialize<SavedStateProto>(serializedState);

    // Recreate the moisture overrides.
    foreach (var moistureOverride in protoState.MoistureOverrides) {
      var index = moistureOverride.OverrideId;
      _moistureLevelOverrides.Add(index, moistureOverride.OverridesList);
      _loadedMoistureOverrideIndexes.Add(index);
    }
    DebugEx.Fine("Loaded {0} moisture overrides", _moistureLevelOverrides.Count);
    _nextMoistureOverrideId = _moistureLevelOverrides.Keys.Any() ? _moistureLevelOverrides.Keys.Max() + 1 : 0;

    // Recreate the contamination overrides.
    foreach (var contaminationOverride in protoState.ContaminationOverrides) {
      var index = contaminationOverride.OverrideId;
      _contaminationOverrides.Add(index, contaminationOverride.OverridesList);
      _loadedContaminationOverrideIndexes.Add(index);
    }
    DebugEx.Fine("Loaded {0} contamination overrides", _contaminationOverrides.Count);
    _nextContaminationOverrideId = _contaminationOverrides.Keys.Any() ? _contaminationOverrides.Keys.Max() + 1 : 0;
  }

  /// <inheritdoc/>
  public void PostLoad() {
    // Unclaimed overrides shouldn't affect the loaded state.
    if (_loadedMoistureOverrideIndexes.Count > 0) {
      DebugEx.Warning("Dropping {0} unclaimed moisture overrides", _loadedMoistureOverrideIndexes.Count);
      foreach (var overrideIndex in _loadedMoistureOverrideIndexes) {
        RemoveMoistureOverride(overrideIndex);
      }
      _loadedMoistureOverrideIndexes.Clear();
    }
    if (_loadedContaminationOverrideIndexes.Count > 0) {
      DebugEx.Warning("Dropping {0} unclaimed contamination overrides", _loadedContaminationOverrideIndexes.Count);
      foreach (var overrideIndex in _loadedContaminationOverrideIndexes) {
        RemoveContaminationOverride(overrideIndex);
      }
      _loadedContaminationOverrideIndexes.Clear();
    }

    // Apply the loaded overrides.
    if (_moistureLevelOverrides.Count > 0) {
      _needMoistureOverridesUpdate = true;
      UpdateMoistureOverrides();
      if (IrrigationSystemSettings.OverrideDesertLevelsForWaterTowers) {
        var terrainMaterialMap = StaticBindings.DependencyContainer.GetInstance<TerrainMaterialMap>();
        terrainMaterialMap.ProcessDesertTextureChanges();
      }
    }
    if (_contaminationOverrides.Count > 0) {
      _needContaminationOverridesUpdate = true;
      UpdateContaminationOverrides();
    }
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
  readonly SoilMoistureService _soilMoistureService;
  readonly BlockService _blockService;
  readonly EventBus _eventBus;
  readonly IThreadSafeColumnTerrainMap _columnTerrainMap;

  SoilOverridesService(MapIndexService mapIndexService, SoilBarrierMap soilBarrierMap,
                       BlockService blockService, EventBus eventBus, ISoilMoistureService soilMoistureService,
                       IThreadSafeColumnTerrainMap columnTerrainMap) {
    _mapIndexService = mapIndexService;
    _soilBarrierMap = soilBarrierMap;
    _blockService = blockService;
    _eventBus = eventBus;
    if (soilMoistureService is not SoilMoistureService soilMoistureServiceConcrete) {
      throw new Exception("Unexpected ISoilMoistureService implementation: " + soilMoistureService.GetType());
    }
    _soilMoistureService = soilMoistureServiceConcrete;
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
      _soilMoistureService.UpdateDesertIntensity(coordinates, _soilMoistureService.SoilMoisture(index3D));
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
    var barrierSpec = e.Entity.GetComponent<SoilBarrierSpec>();
    if (barrierSpec == null || !barrierSpec.BlockContamination) {
      return;
    }
    var blockObject = e.Entity.GetComponent<BlockObject>();
    if (!blockObject.IsFinished || !_contaminatedTilesCache.Contains(blockObject.Coordinates)) {
      return;
    }
    DebugEx.Fine("Restore contamination barrier at: {0}", blockObject.Coordinates);
    _soilBarrierMap.AddContaminationBarrierAt(blockObject.Coordinates);
  }

  #endregion

  #region Game load callbacks

  /// <summary>Called when the game initialized.</summary>
  [OnEvent]
  public void OnNewGameInitialized(ShowPrimaryUIEvent newGameInitializedEvent) {
    GameLoaded = true;
  }

  #endregion
}