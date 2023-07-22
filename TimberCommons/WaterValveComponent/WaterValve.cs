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
// * Variable flow rate toggle
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
  #endregion

  IWaterService _waterService;
  DirectWaterServiceAccessor _directWaterServiceAccessor;

  BlockObject _blockObject;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;
  int _inputTileIndex;
  int _valveBaseZ;

  //FIXME: check the ouptut drop level? can it be palced like this? maybe yes
  internal bool _logExtraStats;
  internal bool _useCustomSimulation = true;

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    enabled = true;
  }

  public override void StartTickable() {
    base.StartTickable();
    _valveBaseZ = _blockObject.Coordinates.z;
    // FIXME: Debug settings
    // _inputCoordinates = new Vector2Int(0, -3);
    // _outputCoordinates = new Vector2Int(0, 4);
    _inputCoordinatesTransformed = _blockObject.Transform(_inputCoordinates);
    _inputTileIndex = _directWaterServiceAccessor.MapIndexService.CoordinatesToIndex(_inputCoordinatesTransformed);
    _outputCoordinatesTransformed = _blockObject.Transform(_outputCoordinates);
    _valveBaseZ = _blockObject.BaseZ;
  }

  // FIXME(IgorZ): Once the debugging is done, set the consumer state once in StartTickable. 
  public override void Tick() {
    // FIXME: Unreliable check of the depths!
    WaterHeightAtInput = Mathf.Max(_waterService.WaterHeight(_inputCoordinatesTransformed), _valveBaseZ);
    WaterHeightAtOutput = Mathf.Max(_waterService.WaterHeight(_outputCoordinatesTransformed), _valveBaseZ);
    if (!_useCustomSimulation || !_directWaterServiceAccessor.IsValid) {
      _directWaterServiceAccessor.DeleteWaterConsumer(_inputTileIndex);
      MoveWaterLight(Time.fixedDeltaTime);
    } else {
      _directWaterServiceAccessor.SetWaterConsumer(_inputTileIndex, FlowLimitSetting);
      _directWaterServiceAccessor.FlushWaterStats(
          _inputTileIndex, out var waterTakenLastTick, out var waterShortageLastTick);
      CurrentFlow = 2 * waterTakenLastTick / Time.fixedDeltaTime;
      _waterService.AddWater(_outputCoordinatesTransformed, waterTakenLastTick);
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
