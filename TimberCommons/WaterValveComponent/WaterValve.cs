// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockSystem;
using Timberborn.Persistence;
using Timberborn.TickSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterValveComponent {

// TODO: settings
// Ingame:
// * Limit overflow (over 1 high) maybe user setting default 1high - NA
// * Backflow - NA
// Component:
// * Enable overflow(or just user setting) - NA
// * Allow backflow
// * Min water lever

/// <summary>Component that moves water from input to output based on the water levels.</summary>
/// <remarks>
/// The water is moved from tiles with a higher level to the tiles with a lover level. The maximum water flow can be
/// limited. Add this component to a water obstacle prefab.
/// </remarks>
public class WaterValve : TickableComponent, IPersistentEntity {
  #region Unity fields
  // ReSharper disable InconsistentNaming

  [SerializeField]
  bool _showUIPanel = true;

  [SerializeField]
  Vector2Int _inputCoordinates = new(0, 0);

  [SerializeField]
  Vector2Int _outputCoordinates = new(0, 2);

  [SerializeField]
  float _waterFlowPerSecond = 1.5f;

  [SerializeField]
  bool _canChangeFlowInGame = false;

  [SerializeField]
  float _minimumInGameFlow = 0;

  [SerializeField]
  internal bool _freeFlow = true;

  // ReSharper restore InconsistentNaming
  #endregion

  #region API
  /// <summary>Absolute height of the water surface above the map at teh valve intake.</summary>
  public float WaterHeightAtInput { get; private set; }

  /// <summary>Absolute height of the water surface above the map at the valve outtake.</summary>
  public float WaterHeightAtOutput { get; private set; }

  /// <summary>Water depth at the valve intake relative to the terrain or the bottom obstacle(s).</summary>
  public float WaterDepthAtIntake { get; private set; }

  /// <summary>Water depth at the valve outtake relative to the terrain or the bottom obstacle(s).</summary>
  public float WaterDepthAtOuttake { get; private set; }

  /// <summary>The current speed of the water movement per second.</summary>
  /// <remarks>
  /// This value get become less than the limit if not enough water supply at the intake, but it must never be above the
  /// <see cref="WaterFlow"/>.
  /// </remarks>
  public float CurrentFlow { get; private set; }

  /// <summary>Absolute limit of the water flow from the prefab.</summary>
  public float FlowLimit => _waterFlowPerSecond;

  /// <summary>Current water flow limit that was adjusted via UI or loaded from the saved state.</summary>
  public float WaterFlow { get; internal set; }

  /// <summary>Indicates that the water is moving in a "natural" way from the higher levels to the lowers.</summary>
  /// <remarks>
  /// If this value is <c>false</c>, then the component is actually a pump that can make the output lever higher than at
  /// the input.
  /// </remarks>
  public bool IsFreeFlow => _freeFlow;

  /// <summary>Indicates that flow limit can be changed via UI panel. It's a prefab setting.</summary>
  public bool CanChangeFlowInGame => _canChangeFlowInGame;

  /// <summary>The minimum flow limit that can be set via UI panel. It's a prefab setting.</summary>
  /// <remarks>The maximum level is <see cref="FlowLimit"/>.</remarks>
  /// <seealso cref="CanChangeFlowInGame"/>
  public float MinimumInGameFlow => _minimumInGameFlow;

  /// <summary>Indicates if the UI panel should be shown when the valve is selected. It's a prefab setting.</summary>
  public bool ShowUIPanel => _showUIPanel;
  #endregion

  IWaterService _waterService;
  DirectWaterServiceAccessor _directWaterServiceAccessor;
  DirectWaterServiceAccessor.WaterMover _waterMover;

  BlockObject _blockObject;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;
  int _valveBaseZ;
  int _inputTileIndex;
  int _outputTileIndex;

  internal bool _logExtraStats;

  //FIXME: it's a debug stuff, drop it on release.
  bool _useCustomSimulation = true;

  void Awake() {
    UpdateAdjustableValuesFromPrefab();
    _blockObject = GetComponentFast<BlockObject>();
    enabled = true;
  }

  public override void StartTickable() {
    if (!_directWaterServiceAccessor.IsValid) {
      throw new InvalidOperationException("WaterValve requires operational DirectWaterServiceAccessor. See the logs!");
    }
    base.StartTickable();
    _valveBaseZ = _blockObject.Coordinates.z;
    _inputCoordinatesTransformed = _blockObject.Transform(_inputCoordinates);
    _outputCoordinatesTransformed = _blockObject.Transform(_outputCoordinates);
    _inputTileIndex = _directWaterServiceAccessor.CoordinatesToIndex(_inputCoordinatesTransformed);
    _outputTileIndex = _directWaterServiceAccessor.CoordinatesToIndex(_outputCoordinatesTransformed);
    _valveBaseZ = _blockObject.Coordinates.z;
  }

  // FIXME(IgorZ): Once the debugging is done, set the consumer state once in StartTickable. 
  public override void Tick() {
    WaterHeightAtInput = Mathf.Max(_waterService.WaterHeight(_inputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtIntake = _directWaterServiceAccessor.WaterDepths[_inputTileIndex];
    WaterHeightAtOutput = Mathf.Max(_waterService.WaterHeight(_outputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtOuttake = _directWaterServiceAccessor.WaterDepths[_outputTileIndex];
    if (!_useCustomSimulation || !_directWaterServiceAccessor.IsValid) {
      if (_waterMover != null) {
        _directWaterServiceAccessor.DeleteWaterMover(_waterMover);
        _waterMover = null;
      }
      MoveWaterLight(Time.fixedDeltaTime);
    } else {
      if (_waterMover == null) {
        _waterMover = new DirectWaterServiceAccessor.WaterMover(
            _directWaterServiceAccessor.CoordinatesToIndex(_inputCoordinatesTransformed),
            _directWaterServiceAccessor.CoordinatesToIndex(_outputCoordinatesTransformed));
        _directWaterServiceAccessor.AddWaterMover(_waterMover);
      }
      _waterMover.WaterFlow = WaterFlow;
      _waterMover.FreeFlow = _freeFlow;
      CurrentFlow = 2 * _waterMover.WaterMoved / Time.fixedDeltaTime;
      _waterMover.WaterMoved = 0;
    }
  }

  void MoveWaterLight(float deltaTime) {
    CurrentFlow = 0;
    var availableWater = WaterHeightAtInput - WaterHeightAtOutput;
    var canMoveWater = Mathf.Min(availableWater, WaterFlow);
    CurrentFlow = canMoveWater / deltaTime;
    var depthChange = canMoveWater / 2;
    _waterService.AddWater(_inputCoordinatesTransformed, depthChange);
    _waterService.AddWater(_outputCoordinatesTransformed, depthChange);
  }

  [Inject]
  public void InjectDependencies(IWaterService waterService, DirectWaterServiceAccessor directWaterServiceAccessor) {
    _waterService = waterService;
    _directWaterServiceAccessor = directWaterServiceAccessor;
  }

  void UpdateAdjustableValuesFromPrefab() {
    WaterFlow = FlowLimit;
  }

  #region IPersistentEntity implementation
  static readonly ComponentKey WaterValveKey = new(typeof(WaterValve).FullName);
  static readonly PropertyKey<float> WaterFlowLimitKey = new(nameof(WaterFlow));

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(WaterValveKey);
    saver.Set(WaterFlowLimitKey, WaterFlow);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(WaterValveKey)) {
      UpdateAdjustableValuesFromPrefab();
      return;
    }
    var state = entityLoader.GetComponent(WaterValveKey);
    WaterFlow = state.GetValueOrNullable(WaterFlowLimitKey) ?? FlowLimit;
  }
  #endregion
}

}
