// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.Settings;
using Timberborn.BaseComponentSystem;
using Timberborn.MapStateSystem;
using Timberborn.Persistence;
using Timberborn.WaterBuildings;
using Timberborn.WaterSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildings;

/// <summary>
/// Component that replaces the stock "WaterOutput". It offers a way to set up the target water level at the spillway.
/// And, optionally, allows adjusting it in game via GUI.
/// </summary>
/// <remarks>
/// This component automatically replaces any stock water output with the settings set to defaults. If a building has
/// this component in the prefab, it will keep the custom settings.
/// </remarks>
sealed class AdjustableWaterOutput(
    IWaterService waterService, IThreadSafeWaterMap threadSafeWaterMap, MapSize mapSize,
    WaterOutputLevelRangeService waterOutputLevelRangeService, WaterOverflowCalculator waterOverflowCalculator)
    : WaterOutput(waterService, threadSafeWaterMap, waterOverflowCalculator), IAwakableComponent, IPersistentEntity {

  const float DamHeight = 0.65f;
  const string FluidDumpPrefabName = "FluidDump";

  #region API

  /// <summary>Maximum possible height of the water under the spillway.</summary>
  public int MaxHeight => _waterCoordinatesTransformed.z;

  /// <summary>Maximum target water level allowed for this output.</summary>
  public int MaxTargetHeight => _waterOutputSpec.OverflowAllowed ? mapSize.TotalSize.z : MaxHeight;

  /// <summary>Maximum target water level that is useful to present in the UI slider.</summary>
  public int MaxSliderTargetHeight => waterOutputLevelRangeService.GetMaxTargetHeight(this);

  /// <summary>Tells if the stock water output allows overflow above the spillway.</summary>
  public bool OverflowAllowed => _waterOutputSpec.OverflowAllowed;

  /// <summary>The current water level at the output.</summary>
  public float CurrentWaterLevel => _threadSafeWaterMap.WaterHeightOrFloor(_waterCoordinatesTransformed);

  /// <summary>Minimum possible height of the water under the spillway.</summary>
  public int MinHeight => !_threadSafeWaterMap.TryGetColumnFloor(_waterCoordinatesTransformed, out var floor)
      ? MaxHeight
      : floor;

  /// <summary>
  /// Difference from the spillway height to the water surface. The output will stop if level goes higher.
  /// </summary>
  /// <seealso cref="SetSpillwayHeightDelta"/>
  public float SpillwayHeightDelta { get; private set; } = -0.1f;  // Once it was a Unity setting.

  /// <summary>Tells if the output should stop when the water level reaches the configured target.</summary>
  public bool LimitOutputLevelEnabled => _limitOutputLevelEnabled ?? !_waterOutputSpec.OverflowAllowed;

  /// <summary>Target water level at the output.</summary>
  public float TargetWaterLevel => MaxTargetHeight + SpillwayHeightDelta;

  /// <summary>Default target water level used when the player enables the output limit.</summary>
  public float DefaultTargetWaterLevel => Mathf.Max(CurrentWaterLevel, _blockObject.CoordinatesAtBaseZ.z + DamHeight);

  /// <summary>Tells if the height marker should be shown when the building is selected.</summary>
  public bool ShowHeightMarker =>
      LimitOutputLevelEnabled
      && (!_isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnMechanicalPumps
          || _isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnFluidDumps);

  /// <summary>Tells if GUI should be presented to change the limit in the game.</summary>
  public bool AllowAdjustmentsInGame =>
      !_isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnMechanicalPumps
      || _isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnFluidDumps;

  /// <summary>Transformed coordinates of the water spillway.</summary>
  public Vector3Int TargetCoordinates => _waterCoordinatesTransformed;

  /// <summary>Sets a new spillway delta.</summary>
  /// <remarks>
  /// The new value must make sense in terms of the building Z-coordinate and the water level at the spillway
  /// coordinates. If it doesn't, it will be corrected on the next call to <see cref="CalculateAvailableSpace"/>.
  /// </remarks>
  /// <param name="spillwayDelta"></param>
  /// <seealso cref="SpillwayHeightDelta"/>
  public void SetSpillwayHeightDelta(float spillwayDelta) {
    SpillwayHeightDelta = spillwayDelta;
  }

  /// <summary>Sets whether this output should limit the water level.</summary>
  public void SetLimitOutputLevelEnabled(bool enabled) {
    if (enabled && !LimitOutputLevelEnabled) {
      SetTargetWaterLevel(DefaultTargetWaterLevel);
    }
    _limitOutputLevelEnabled = enabled;
  }

  /// <summary>Sets the target water level.</summary>
  public void SetTargetWaterLevel(float targetWaterLevel) {
    SpillwayHeightDelta = Mathf.Clamp(targetWaterLevel, MinHeight, MaxTargetHeight) - MaxTargetHeight;
  }

  #endregion

  #region Implementation

  bool _isFluidDump;
  bool? _limitOutputLevelEnabled;

  /// <summary>
  /// Called via a Harmony patch to provide  <see cref="WaterOutput.AvailableSpace"/>. Normally happens at least once
  /// per tick. Can be called multiple times, so keep it simple.
  /// </summary>
  internal float CalculateAvailableSpace() {
    if (!LimitOutputLevelEnabled) {
      return CalculateStockAvailableSpace();
    }
    if (SpillwayHeightDelta < MinHeight - MaxTargetHeight) {
      var oldDelta = SpillwayHeightDelta;
      SetSpillwayHeightDelta(MinHeight - MaxTargetHeight);
      HostedDebugLog.Fine(this, "SpillwayHeightDelta corrected: {0} => {1}", oldDelta, SpillwayHeightDelta);
    }
    return TargetWaterLevel - _threadSafeWaterMap.WaterHeightOrFloor(_waterCoordinatesTransformed);
  }

  float CalculateStockAvailableSpace() {
    if (!_waterOutputSpec.OverflowAllowed) {
      return DistanceToGround;
    }
    var currentOverflow = _threadSafeWaterMap.ColumnOverflow(_waterCoordinatesTransformed);
    if (currentOverflow <= 0f) {
      return float.MaxValue;
    }
    var ceiling = _threadSafeWaterMap.ColumnCeiling(_waterCoordinatesTransformed);
    return _waterOverflowCalculator.GetOverflowSpace(currentOverflow, ceiling) - WaterUpperSafetySpace;
  }

  /// <inheritdoc/>
  public new void Awake() {
    base.Awake();
    _isFluidDump = Name.StartsWith(FluidDumpPrefabName);
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey AdjustableWaterOutputKey = new(typeof(AdjustableWaterOutput).FullName);
  static readonly PropertyKey<float> SpillwayHeightDeltaKey = new("SpillwayHeightDelta");
  static readonly PropertyKey<bool> LimitOutputLevelEnabledKey = new("LimitOutputLevelEnabled");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var component = entitySaver.GetComponent(AdjustableWaterOutputKey);
    component.Set(SpillwayHeightDeltaKey, SpillwayHeightDelta);
    component.Set(LimitOutputLevelEnabledKey, LimitOutputLevelEnabled);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(AdjustableWaterOutputKey, out var component)) {
      return;
    }
    if (component.Has(SpillwayHeightDeltaKey)) {
      SpillwayHeightDelta = component.Get(SpillwayHeightDeltaKey);
    }
    if (component.Has(LimitOutputLevelEnabledKey)) {
      _limitOutputLevelEnabled = component.Get(LimitOutputLevelEnabledKey);
    }
  }

  #endregion
}
