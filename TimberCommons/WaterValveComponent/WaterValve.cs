// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockSystem;
using Timberborn.MapIndexSystem;
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

/// <summary>Component that moves water from input to output based on the water levels.</summary>
/// <remarks>
/// The water is moved from tiles with a higher level to the tiles with a lover level. The maximum water flow can be
/// limited. Add this component to a water obstacle prefab.
/// </remarks>
public class WaterValve : TickableComponent, IPersistentEntity {
  #region Unity fields
  // ReSharper disable InconsistentNaming

  [SerializeField]
  bool _showUIPanel = false;

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
  bool _freeFlow = true;

  [SerializeField]
  float _minimumWaterLevelAtIntake = 0.2f;

  [SerializeField]
  float _maximumWaterLevelAtOuttake = 0.6f;

  // ReSharper restore InconsistentNaming
  #endregion

  #region API
  /// <summary>Absolute height of the water surface above the map at teh valve intake.</summary>
  public float WaterHeightAtInput { get; private set; }

  /// <summary>Absolute height of the water surface above the map at the valve outtake.</summary>
  public float WaterHeightAtOutput { get; private set; }

  /// <summary>Water depth at the valve intake relative to the terrain or the bottom obstacle(s).</summary>
  public float WaterDepthAtIntake { get; private set; }

  /// <summary>The minimum level of water to maintain at the input.</summary>
  /// <remarks>The valve won't take water if the level is blow the setting. Values below zero mean "no limit".</remarks>
  public float MinWaterLevelAtIntake {
    get => _minimumWaterLevelAtIntake;
    internal set {
      _minimumWaterLevelAtIntake = value;
      _waterMover.MinHeightAtInput = value > 0 ? value + _valveBaseZ : -1.0f;
    }
  }

  /// <summary>The maximum level of water to maintain at the output.</summary>
  /// <remarks>
  /// The valve won't move water if the level is above the setting. Values below zero mean "no limit".
  /// </remarks>
  //public float MaxWaterLevelAtOuttake => _maximumWaterLevelAtOuttake;
  public float MaxWaterLevelAtOuttake {
    get => _maximumWaterLevelAtOuttake;
    internal set {
      _maximumWaterLevelAtOuttake = value;
      _waterMover.MaxHeightAtOutput = value > 0 ? value + _valveBaseZ : -1.0f;
    }
  }

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
  /// <remarks>It can be adjusted interactively during the game if <see cref="CanChangeFlowInGame"/> is set.</remarks>
  public float WaterFlow {
    get => _waterMover.WaterFlow;
    internal set => _waterMover.WaterFlow = value;
  }

  /// <summary>Indicates that the water is moving in a "natural" way from the higher levels to the lowers.</summary>
  /// <remarks>
  /// If this value is <c>false</c>, then the component is actually a pump that can make the output lever higher than at
  /// the input.
  /// </remarks>
  public bool IsFreeFlow {
    get => _waterMover.FreeFlow;
    internal set => _waterMover.FreeFlow = value;
  }

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
  MapIndexService _mapIndexService;
  readonly DirectWaterServiceAccessor.WaterMover _waterMover = new();

  BlockObject _blockObject;
  int _valveBaseZ;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;

  public bool LogExtraStats {
    get => _waterMover.LogExtraStats;
    internal set => _waterMover.LogExtraStats = value;
  }

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    UpdateAdjustableValuesFromPrefab();
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
    _waterMover.InputTileIndex = _mapIndexService.CoordinatesToIndex(_inputCoordinatesTransformed);
    _waterMover.OutputTileIndex = _mapIndexService.CoordinatesToIndex(_outputCoordinatesTransformed);
    MinWaterLevelAtIntake = _minimumWaterLevelAtIntake;
    MaxWaterLevelAtOuttake = _maximumWaterLevelAtOuttake;
    _directWaterServiceAccessor.AddWaterMover(_waterMover);
  }

  public override void Tick() {
    WaterHeightAtInput = Mathf.Max(_waterService.WaterHeight(_inputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtIntake = _directWaterServiceAccessor.WaterDepths[_waterMover.InputTileIndex];
    WaterHeightAtOutput = Mathf.Max(_waterService.WaterHeight(_outputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtOuttake = _directWaterServiceAccessor.WaterDepths[_waterMover.OutputTileIndex];
    CurrentFlow = 2 * _waterMover.WaterMoved / Time.fixedDeltaTime;
    _waterMover.WaterMoved = 0;
  }

  [Inject]
  public void InjectDependencies(IWaterService waterService, DirectWaterServiceAccessor directWaterServiceAccessor,
                                 MapIndexService mapIndexService) {
    _waterService = waterService;
    _directWaterServiceAccessor = directWaterServiceAccessor;
    _mapIndexService = mapIndexService;
  }

  void UpdateAdjustableValuesFromPrefab() {
    WaterFlow = FlowLimit;
    IsFreeFlow = _freeFlow;
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
