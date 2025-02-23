// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.Settings;
using Timberborn.Persistence;
using Timberborn.PrefabSystem;
using Timberborn.WaterBuildings;
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
sealed class AdjustableWaterOutput : WaterOutput, IPersistentEntity {

  #region Fields for Unity
  // ReSharper disable InconsistentNaming

  [SerializeField]
  [Tooltip("A minimum height difference between the output spillway and the water surface below.")]
  float _spillwayHeightDelta = 0.1f;
  
  [SerializeField]
  [Tooltip("Tells if there should be a controls in the game GUI to change the depth limit. Ths GUI is always available"
      + " in DEV mode.")]
  bool _allowAdjustmentsInGame = true;

  [SerializeField]
  [Tooltip("Tells if currently selected height should be displayed the same ways as it's done for Sluice.")]
  bool _showHeightMarker = true;

  // ReSharper restore InconsistentNaming
  #endregion

  #region API

  /// <summary>Maximum possible height of the water under the spillway.</summary>
  public int MaxHeight => _waterCoordinatesTransformed.z;

  /// <summary>Minimum possible height of the water under the spillway.</summary>
  public int MinHeight => !_threadSafeWaterMap.TryGetColumnFloor(_waterCoordinatesTransformed, out var floor)
      ? MaxHeight
      : floor;

  /// <summary>
  /// Difference from the spillway height to the water surface. The output will stop if level goes higher.
  /// </summary>
  /// <seealso cref="SetSpillwayHeightDelta"/>
  public float SpillwayHeightDelta { get; private set; }

  /// <summary>Tells if the height marker should be shown when the building is selected.</summary>
  public bool ShowHeightMarker =>
      _showHeightMarker
      && (!_isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnMechanicalPumps
          || _isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnFluidDumps);

  /// <summary>Tells if GUI should be presented to change the limit in the game.</summary>
  public bool AllowAdjustmentsInGame =>
      _allowAdjustmentsInGame
      && (!_isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnMechanicalPumps
          || _isFluidDump && WaterBuildingsSettings.AdjustWaterDepthAtSpillwayOnFluidDumps);

  /// <summary>Transformed coordinates of the water spillway.</summary>
  public Vector3Int TargetCoordinates => _waterCoordinatesTransformed;

  /// <summary>Sets a new spillway delta.</summary>
  /// <remarks>
  /// The new value must make sense in terms of the building Z-coordinate and the water level at teh spillway
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

  new void Awake() {
    SpillwayHeightDelta = -_spillwayHeightDelta;
    base.Awake();
    //FIXME
    _isFluidDump = GetComponentFast<PrefabSpec>().name.StartsWith("FluidDump");
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
    if (!entityLoader.HasComponent(AdjustableWaterOutputKey)) {
      return;
    }
    var component = entityLoader.GetComponent(AdjustableWaterOutputKey);
    if (component.Has(SpillwayHeightDeltaKey)) {
      SpillwayHeightDelta = component.Get(SpillwayHeightDeltaKey);
    }
  }

  #endregion
}
