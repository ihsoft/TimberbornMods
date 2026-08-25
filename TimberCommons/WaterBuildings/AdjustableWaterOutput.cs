// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.Settings;
using Timberborn.BaseComponentSystem;
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
sealed class AdjustableWaterOutput(IWaterService waterService, IThreadSafeWaterMap threadSafeWaterMap)
    : WaterOutput(waterService, threadSafeWaterMap), IAwakableComponent, IPersistentEntity {

  const string FluidDumpPrefabName = "FluidDump";

  #region API

  /// <summary>Maximum possible height of the water under the spillway.</summary>
  public int MaxHeight => _waterCoordinatesTransformed.z;

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

  /// <summary>Tells if the height marker should be shown when the building is selected.</summary>
  public bool ShowHeightMarker =>
      !_isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnMechanicalPumps
      || _isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnFluidDumps;

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

  #endregion

  #region Implementation

  bool _isFluidDump;

  /// <summary>
  /// Called via a Harmony patch to provide  <see cref="WaterOutput.AvailableSpace"/>. Normally happens at least once
  /// per tick. Can be called multiple times, so keep it simple.
  /// </summary>
  internal float CalculateAvailableSpace() {
    if (SpillwayHeightDelta < MinHeight - MaxHeight) {
      var oldDelta = SpillwayHeightDelta;
      SetSpillwayHeightDelta(MinHeight - MaxHeight);
      HostedDebugLog.Fine(this, "SpillwayHeightDelta corrected: {0} => {1}", oldDelta, SpillwayHeightDelta);
    }
    return MaxHeight + SpillwayHeightDelta - _threadSafeWaterMap.WaterHeightOrFloor(_waterCoordinatesTransformed);
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

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var component = entitySaver.GetComponent(AdjustableWaterOutputKey);
    component.Set(SpillwayHeightDeltaKey, SpillwayHeightDelta);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(AdjustableWaterOutputKey, out var component)) {
      return;
    }
    if (component.Has(SpillwayHeightDeltaKey)) {
      SpillwayHeightDelta = component.Get(SpillwayHeightDeltaKey);
    }
  }

  #endregion
}
