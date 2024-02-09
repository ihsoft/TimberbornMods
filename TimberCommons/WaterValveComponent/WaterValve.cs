// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.ConstructibleSystem;
using Timberborn.MapIndexSystem;
using Timberborn.Particles;
using Timberborn.Persistence;
using Timberborn.TickSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterValveComponent {

/// <summary>Component that moves water from input to output based on the water levels.</summary>
/// <remarks>
/// The water is moved from tiles with a higher level to the tiles with a lover level. The maximum water flow can be
/// limited. Add this component to a water obstacle prefab.
/// </remarks>
public sealed class WaterValve : TickableComponent, IPersistentEntity, IFinishedStateListener, IPausableComponent {
  #region Unity fields
  // ReSharper disable InconsistentNaming
  // ReSharper disable RedundantDefaultMemberInitializer

  [SerializeField]
  [Tooltip("Indicates if the valve UI fragment should be shown in game for this building.")]
  bool _showUIPanel = true;

  [SerializeField]
  [Tooltip("Relative coordinates at which the water will be taken.")]
  Vector2Int _inputCoordinates = new(0, 0);

  [SerializeField]
  [Tooltip("Relative coordinates at which the water will be dropped.")]
  Vector2Int _outputCoordinates = new(0, 2);

  [SerializeField]
  [Tooltip("Maximum water flow in cubic metres per second.")]
  float _waterFlowPerSecond = 1.5f;

  [SerializeField]
  [Tooltip("Indicates if the flow rate can be adjust in game by the player.")]
  bool _canChangeFlowInGame = true;

  [SerializeField]
  [Tooltip("If the flow change is allowed, then this will be the minimum possible value to set.")]
  float _minimumInGameFlow = 0;

  [SerializeField]
  [Tooltip("Don't take water at input below this threshold. Set to -1 to disable this check.")]
  float _minimumWaterLevelAtIntake = 0.2f;

  [SerializeField]
  [Tooltip("Don't drop water at output above this threshold. Set to -1 to disable this check.")]
  float _maximumWaterLevelAtOuttake = 0.6f;

  [SerializeField]
  [Tooltip("Indicates that water level check at the output can be disabled via UI.")]
  bool _allowDisablingOutputLevelCheck = true;

  [SerializeField]
  [Tooltip("Tells if the contamination should also be moved.")]
  bool _moveContaminatedWater = true;

  [SerializeField]
  [Tooltip("If set, then will start on non-zero flow and stop if no water is being moving via the valve.")]
  ParticleSystem _particleSystem = null;

  // ReSharper restore RedundantDefaultMemberInitializer
  // ReSharper restore InconsistentNaming
  #endregion

  #region API

  /// <summary>Absolute height of the water surface above the map at the valve intake.</summary>
  public float WaterHeightAtInput { get; private set; }

  /// <summary>Absolute height of the water surface above the map at the valve outtake.</summary>
  public float WaterHeightAtOutput { get; private set; }

  /// <summary>Water depth at the valve intake relative to the terrain or the bottom obstacle(s).</summary>
  public float WaterDepthAtIntake { get; private set; }

  /// <summary>Water depth at the valve outtake relative to the terrain or the bottom obstacle(s).</summary>
  public float WaterDepthAtOuttake { get; private set; }

  /// <summary>The minimum level of water to maintain at the input.</summary>
  /// <remarks>The valve won't take water if the level is blow the setting. Values below zero mean "no limit".</remarks>
  public float MinWaterLevelAtIntake {
    get => _minimumWaterLevelAtIntake;
    set {
      _minimumWaterLevelAtIntake = value;
      UpdateWaterMover();
    }
  }

  /// <summary>The maximum level of water to maintain at the output.</summary>
  /// <remarks>
  /// The valve won't move water if the level is above the setting. Values below zero mean "no limit".
  /// </remarks>
  /// <seealso cref="OutputLevelCheckDisabled"/>
  public float MaxWaterLevelAtOuttake {
    get => _maximumWaterLevelAtOuttake;
    set {
      _maximumWaterLevelAtOuttake = value;
      UpdateWaterMover();
    }
  }

  /// <summary>The actual speed of the water movement per second.</summary>
  /// <remarks>
  /// This value is not a constant and changes based on the water supply at the intake and the available space at the
  /// outtake. It can be zero, but cannot be negative or exceed the <see cref="WaterFlow"/> setting.
  /// </remarks>
  public float CurrentFlow { get; private set; }

  /// <summary>Absolute limit of the water flow from the prefab.</summary>
  /// <seealso cref="WaterFlow"/>
  public float FlowLimit => _waterFlowPerSecond;

  /// <summary>Current water flow limit setting that was adjusted via UI or loaded from the saved state.</summary>
  /// <remarks>It can be adjusted interactively during the game if <see cref="CanChangeFlowInGame"/> is set.</remarks>
  /// <seealso cref="FlowLimit"/>
  public float WaterFlow {
    get => _waterMover.WaterFlow;
    set => _waterMover.WaterFlow = value;
  }

  /// <summary>Indicates that flow limit can be changed via UI panel. It's a prefab setting.</summary>
  /// <seealso cref="MinimumInGameFlow"/>
  public bool CanChangeFlowInGame => _canChangeFlowInGame;

  /// <summary>The minimum flow limit that can be set via UI panel. It's a prefab setting.</summary>
  /// <remarks>The maximum level is <see cref="FlowLimit"/>.</remarks>
  /// <seealso cref="CanChangeFlowInGame"/>
  public float MinimumInGameFlow => _minimumInGameFlow;

  /// <summary>Indicates that water level check at the output can be disabled via UI.</summary>
  /// <seealso cref="MaxWaterLevelAtOuttake"/>
  /// <seealso cref="OutputLevelCheckDisabled"/>
  public bool CanDisableOutputLevelCheck => _allowDisablingOutputLevelCheck;

  /// <summary>Tells if the output water level check is being performed by the valve.</summary>
  /// <remarks>
  /// This setting depends on <see cref="MaxWaterLevelAtOuttake"/>. If the maximum water level is not set, then this
  /// setting has no effect.
  /// </remarks>
  /// <seealso cref="MaxWaterLevelAtOuttake"/>
  public bool OutputLevelCheckDisabled {
    get => _outputLevelCheckDisabled;
    set {
      _outputLevelCheckDisabled = value;
      UpdateWaterMover();
    }
  }
  bool _outputLevelCheckDisabled;

  /// <summary>Tells if the contamination should also be moved.</summary>
  /// <remarks>If not set, then the valve works as a filter and only clear water is emitted at the drop.</remarks>
  public bool MoveContaminatedWater {
    get => _moveContaminatedWater;
    set {
      _moveContaminatedWater = value;
      UpdateWaterMover();
    }
  }

  /// <summary>Indicates if the UI panel should be shown when the valve is selected. It's a prefab setting.</summary>
  public bool ShowUIPanel => _showUIPanel;

  /// <summary>Tells if the valve is active and processing water.</summary>
  public bool IsActive { get; private set; }
  #endregion

  #region Implementation

  IWaterService _waterService;
  DirectWaterServiceAccessor _directWaterServiceAccessor;
  MapIndexService _mapIndexService;
  ParticlesRunnerFactory _particlesRunnerFactory;

  /// <summary>Changes to the mover will be propagated to the simulation system on the next tick.</summary>
  /// <seealso cref="UpdateWaterMover"/>
  readonly DirectWaterServiceAccessor.WaterMover _waterMover = new();

  BlockObject _blockObject;
  PausableBuilding _pausableBuilding;
  ParticlesRunner _particlesRunner;
  int _valveBaseZ;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    _pausableBuilding = GetComponentFast<PausableBuilding>();
    if (_particleSystem != null) {
      _particlesRunner = _particlesRunnerFactory.CreateForFinishedState(GameObjectFast, _particleSystem);
    }
    _waterMover.WaterFlow = _waterFlowPerSecond;
  }

  /// <summary>Injected instances.</summary>
  [Inject]
  public void InjectDependencies(IWaterService waterService, DirectWaterServiceAccessor directWaterServiceAccessor,
                                 MapIndexService mapIndexService, ParticlesRunnerFactory particlesRunnerFactory) {
    _waterService = waterService;
    _directWaterServiceAccessor = directWaterServiceAccessor;
    _mapIndexService = mapIndexService;
    _particlesRunnerFactory = particlesRunnerFactory;
  }

  /// <summary>Adds water mover to the simulation system.</summary>
  void StartWaterMover() {
    if (IsActive) {
      StopWaterMover();
    }
    UpdateWaterMover();
    _directWaterServiceAccessor.AddWaterMover(_waterMover);
    IsActive = true;
  }

  /// <summary>Removes water mover from the simulation system.</summary>
  void StopWaterMover() {
    if (!IsActive) {
      return;
    }
    IsActive = false;
    _directWaterServiceAccessor.DeleteWaterMover(_waterMover);
    if (_particlesRunner != null) {
      _particlesRunner.Stop();
    }
  }

  /// <summary>Updates water mover to the current settings.</summary>
  void UpdateWaterMover() {
    _waterMover.MinHeightAtInput = _minimumWaterLevelAtIntake > 0 ? _minimumWaterLevelAtIntake + _valveBaseZ : -1.0f;
    _waterMover.MaxHeightAtOutput = !OutputLevelCheckDisabled && _maximumWaterLevelAtOuttake > 0
        ? _maximumWaterLevelAtOuttake + _valveBaseZ
        : -1.0f;
    _waterMover.MoveContaminatedWater = _moveContaminatedWater;
  }

  #endregion

  #region TickableComponent implemenatation

  /// <inheritdoc/>
  public override void Tick() {
    if (!IsActive) {
      return;
    }
    WaterHeightAtInput = Mathf.Max(_waterService.WaterHeight(_inputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtIntake = _directWaterServiceAccessor.WaterDepths[_waterMover.InputTileIndex];
    WaterHeightAtOutput = Mathf.Max(_waterService.WaterHeight(_outputCoordinatesTransformed), _valveBaseZ);
    WaterDepthAtOuttake = _directWaterServiceAccessor.WaterDepths[_waterMover.OutputTileIndex];
    CurrentFlow = 2 * _waterMover.WaterMoved / Time.fixedDeltaTime;
    _waterMover.WaterMoved = 0;
    if (_particlesRunner != null) {
      if (CurrentFlow > float.Epsilon) {
        _particlesRunner.Play();
      } else {
        _particlesRunner.Stop();
      }
    }
  }

  #endregion

  #region IPersistentEntity implementation

  static readonly ComponentKey WaterValveKey = new(typeof(WaterValve).FullName);
  static readonly PropertyKey<float> WaterFlowLimitKey = new(nameof(WaterFlow));
  static readonly PropertyKey<bool> OutputLevelCheckDisabledKey = new(nameof(OutputLevelCheckDisabled));
  static readonly PropertyKey<bool> MoveContaminatedWaterKey = new(nameof(MoveContaminatedWater));

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var saver = entitySaver.GetComponent(WaterValveKey);
    saver.Set(WaterFlowLimitKey, WaterFlow);
    saver.Set(OutputLevelCheckDisabledKey, OutputLevelCheckDisabled);
    saver.Set(MoveContaminatedWaterKey, MoveContaminatedWater);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(WaterValveKey)) {
      return;
    }
    var state = entityLoader.GetComponent(WaterValveKey);
    WaterFlow = state.GetValueOrNullable(WaterFlowLimitKey) ?? FlowLimit;
    _outputLevelCheckDisabled = state.GetValueOrNullable(OutputLevelCheckDisabledKey) ?? _outputLevelCheckDisabled;
    _moveContaminatedWater = state.GetValueOrNullable(MoveContaminatedWaterKey) ?? _moveContaminatedWater;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    _valveBaseZ = _blockObject.Coordinates.z;
    _inputCoordinatesTransformed = _blockObject.Transform(_inputCoordinates);
    _outputCoordinatesTransformed = _blockObject.Transform(_outputCoordinates);
    _waterMover.InputTileIndex = _mapIndexService.CoordinatesToIndex(_inputCoordinatesTransformed);
    _waterMover.OutputTileIndex = _mapIndexService.CoordinatesToIndex(_outputCoordinatesTransformed);
    _pausableBuilding.PausedChanged += (_, _) => {
      if (_pausableBuilding.Paused) {
        StopWaterMover();
      } else {
        StartWaterMover();
      }
    };
    if (!_pausableBuilding.Paused) {
      StartWaterMover();
    }
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    StopWaterMover();
  }

  #endregion
}

}
