// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

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

  // ReSharper restore InconsistentNaming
  #endregion

  #region API
  public float WaterHeightAtInput { get; private set; }
  public float WaterHeightAtOutput { get; private set; }
  public float CurrentFlow { get; private set; }
  public float FlowLimit => _waterFlowPerSecond;
  public float FlowLimitSetting { get; internal set; }
  public bool CanChangeFlowInGame => _canChangeFlowInGame;
  public float MinimumInGameFlow => _minimumInGameFlow;
  public bool ShowUIPanel => _showUIPanel;
  #endregion

  IWaterService _waterService;
  DirectWaterServiceAccessor _directWaterServiceAccessor;
  DirectWaterServiceAccessor.WaterMover _waterMover;

  BlockObject _blockObject;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;
  int _valveBaseZ;

  internal bool _logExtraStats;
  internal bool _useCustomSimulation = true;

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    enabled = true;
  }

  public override void StartTickable() {
    base.StartTickable();
    _valveBaseZ = _blockObject.Coordinates.z;
    _inputCoordinatesTransformed = _blockObject.Transform(_inputCoordinates);
    _outputCoordinatesTransformed = _blockObject.Transform(_outputCoordinates);
    _valveBaseZ = _blockObject.BaseZ;
  }

  // FIXME(IgorZ): Once the debugging is done, set the consumer state once in StartTickable. 
  public override void Tick() {
    WaterHeightAtInput = Mathf.Max(_waterService.WaterHeight(_inputCoordinatesTransformed), _valveBaseZ);
    WaterHeightAtOutput = Mathf.Max(_waterService.WaterHeight(_outputCoordinatesTransformed), _valveBaseZ);
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
            _directWaterServiceAccessor.CoordinatesToIndex(_outputCoordinatesTransformed)) {
            FreeFlow = true,
        };
        _directWaterServiceAccessor.AddWaterMover(_waterMover);
      }
      _waterMover.WaterFlow = FlowLimitSetting;
      CurrentFlow = 2 * _waterMover.WaterMoved / Time.fixedDeltaTime;
      _waterMover.WaterMoved = 0;
    }
  }

  void MoveWaterLight(float deltaTime) {
    CurrentFlow = 0;
    var availableWater = WaterHeightAtInput - WaterHeightAtOutput;
    var canMoveWater = Mathf.Min(availableWater, FlowLimitSetting);
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

  #region IPersistentEntity implementation
  static readonly ComponentKey WaterValveKey = new(typeof(WaterValve).FullName);
  static readonly PropertyKey<float> WaterFlowLimitKey = new(nameof(FlowLimitSetting));

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(WaterValveKey);
    saver.Set(WaterFlowLimitKey, FlowLimitSetting);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(WaterValveKey)) {
      FlowLimitSetting = _waterFlowPerSecond;
      return;
    }
    var state = entityLoader.GetComponent(WaterValveKey);
    FlowLimitSetting = state.GetValueOrNullable(WaterFlowLimitKey) ?? _waterFlowPerSecond;
  }
  #endregion
}

}
