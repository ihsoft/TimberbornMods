// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.TimberCommons.Settings;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildings;

sealed class WaterOutputLevelRangeService(
    ITerrainService terrainService,
    EntityComponentRegistry entityComponentRegistry,
    EventBus eventBus) : ILoadableSingleton, IUnloadableSingleton {

  #region API

  public int GetMaxTargetHeight(AdjustableWaterOutput waterOutput) {
    if (!waterOutput.OverflowAllowed) {
      return waterOutput.MaxTargetHeight;
    }
    var usefulMaxTargetHeight = WaterBuildingsSettings.UseLocalOutputLevelLimitScan
        ? GetLocalMaxTargetHeight(new Vector2Int(waterOutput.TargetCoordinates.x, waterOutput.TargetCoordinates.y))
        : GetGlobalMaxTargetHeight();
    var minimumUsefulHeight = Mathf.CeilToInt(Mathf.Max(
        waterOutput.TargetWaterLevel,
        waterOutput.CurrentWaterLevel,
        waterOutput.DefaultTargetWaterLevel,
        waterOutput.MinHeight));
    return Mathf.Clamp(
        Mathf.Max(usefulMaxTargetHeight, minimumUsefulHeight), waterOutput.MinHeight, waterOutput.MaxTargetHeight);
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
    terrainService.MinMaxTerrainHeightChanged += OnMinMaxTerrainHeightChanged;
    eventBus.Register(this);
  }

  /// <inheritdoc/>
  public void Unload() {
    terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
    terrainService.MinMaxTerrainHeightChanged -= OnMinMaxTerrainHeightChanged;
    eventBus.Unregister(this);
  }

  #endregion

  #region Implementation

  record struct LocalCacheKey(Vector2Int Center, int Radius);

  readonly Dictionary<LocalCacheKey, int> _localMaxTargetHeights = [];
  int? _globalMaxTargetHeight;

  int GetLocalMaxTargetHeight(Vector2Int center) {
    var radius = WaterBuildingsSettings.OutputLevelLimitScanRadius;
    var cacheKey = new LocalCacheKey(center, radius);
    if (_localMaxTargetHeights.TryGetValue(cacheKey, out var cachedHeight)) {
      return cachedHeight;
    }
    var maxTargetHeight = CalculateLocalMaxTargetHeight(center, radius);
    _localMaxTargetHeights.Add(cacheKey, maxTargetHeight);
    return maxTargetHeight;
  }

  int GetGlobalMaxTargetHeight() {
    if (_globalMaxTargetHeight.HasValue) {
      return _globalMaxTargetHeight.Value;
    }
    _globalMaxTargetHeight = CalculateGlobalMaxTargetHeight();
    return _globalMaxTargetHeight.Value;
  }

  int CalculateLocalMaxTargetHeight(Vector2Int center, int radius) {
    var maxHeight = 0;
    for (var y = center.y - radius; y <= center.y + radius; y++) {
      for (var x = center.x - radius; x <= center.x + radius; x++) {
        var coordinates = new Vector2Int(x, y);
        if (!terrainService.Contains(coordinates)) {
          continue;
        }
        maxHeight = Math.Max(maxHeight, terrainService.GetTerrainHeight(new Vector3Int(x, y, 0)));
      }
    }
    return Math.Max(maxHeight, GetMaxBlockHeight(center, radius));
  }

  int CalculateGlobalMaxTargetHeight() {
    return Math.Max(terrainService.MaxTerrainHeight, GetMaxBlockHeight());
  }

  int GetMaxBlockHeight(Vector2Int center, int radius) {
    var maxHeight = 0;
    foreach (var building in entityComponentRegistry.GetEnabled<Building>()) {
      var blockObject = building.GetComponent<BlockObject>();
      if (!ShouldCountBlockObject(blockObject)) {
        continue;
      }
      foreach (var coordinates in blockObject.PositionedBlocks.GetOccupiedCoordinates()) {
        if (Math.Abs(coordinates.x - center.x) > radius || Math.Abs(coordinates.y - center.y) > radius) {
          continue;
        }
        maxHeight = Math.Max(maxHeight, coordinates.z + 1);
      }
    }
    return maxHeight;
  }

  int GetMaxBlockHeight() {
    var maxHeight = 0;
    foreach (var building in entityComponentRegistry.GetEnabled<Building>()) {
      var blockObject = building.GetComponent<BlockObject>();
      if (!ShouldCountBlockObject(blockObject)) {
        continue;
      }
      foreach (var coordinates in blockObject.PositionedBlocks.GetOccupiedCoordinates()) {
        maxHeight = Math.Max(maxHeight, coordinates.z + 1);
      }
    }
    return maxHeight;
  }

  static bool ShouldCountBlockObject(BlockObject blockObject) {
    return blockObject && !blockObject.IsPreview && blockObject.Positioned;
  }

  void InvalidateCache() {
    _localMaxTargetHeights.Clear();
    _globalMaxTargetHeight = null;
  }

  void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs e) {
    InvalidateCache();
  }

  void OnMinMaxTerrainHeightChanged(object sender, EventArgs e) {
    InvalidateCache();
  }

  [OnEvent]
  public void OnBlockObjectSet(BlockObjectSetEvent e) {
    InvalidateCache();
  }

  [OnEvent]
  public void OnBlockObjectUnset(BlockObjectUnsetEvent e) {
    InvalidateCache();
  }

  #endregion
}
