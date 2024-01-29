// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockSystem;
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
public sealed class WaterValve : TickableComponent, IPersistentEntity, IFinishedStateListener {
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
  bool _canChangeFlowInGame = true;

  [SerializeField]
  float _minimumInGameFlow = 0;

  [SerializeField]
  float _minimumWaterLevelAtIntake = 0.2f;

  [SerializeField]
  float _maximumWaterLevelAtOuttake = 0.6f;

  [SerializeField]
  bool _allowDisablingOutputLevelCheck = true;

  [SerializeField]
  bool _moveContaminatedWater = true;

  [SerializeField]
  ParticleSystem _particleSystem = null;

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
    internal set {
      _minimumWaterLevelAtIntake = value;
      _waterMover.MinHeightAtInput = value > 0 ? value + _valveBaseZ : -1.0f;
    }
  }

  /// <summary>The maximum level of water to maintain at the output.</summary>
  /// <remarks>
  /// The valve won't move water if the level is above the setting. Values below zero mean "no limit".
  /// </remarks>
  /// <seealso cref="OutputLevelCheckDisabled"/>
  public float MaxWaterLevelAtOuttake {
    get => _maximumWaterLevelAtOuttake;
    internal set {
      _maximumWaterLevelAtOuttake = value;
      _waterMover.MaxHeightAtOutput = !OutputLevelCheckDisabled && value > 0 ? value + _valveBaseZ : -1.0f;
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
    internal set => _waterMover.WaterFlow = value;
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
      MaxWaterLevelAtOuttake = MaxWaterLevelAtOuttake; // Refresh water mover.
    }
  }
  bool _outputLevelCheckDisabled;

  /// <summary>Tells if the contamination should also be moved.</summary>
  /// <remarks>If not set, then the valve works as a filter and only clear water is emitted at the drop.</remarks>
  public bool MoveContaminatedWater {
    get => _moveContaminatedWater;
    set {
      _moveContaminatedWater = value;
      _waterMover.MoveContaminatedWater = value;
    }
  }

  /// <summary>Indicates if the UI panel should be shown when the valve is selected. It's a prefab setting.</summary>
  public bool ShowUIPanel => _showUIPanel;
  #endregion

  #region Implementation
  IWaterService _waterService;
  DirectWaterServiceAccessor _directWaterServiceAccessor;
  MapIndexService _mapIndexService;
  ParticlesRunnerFactory _particlesRunnerFactory;

  readonly DirectWaterServiceAccessor.WaterMover _waterMover = new();

  BlockObject _blockObject;
  ParticlesRunner _particlesRunner;
  int _valveBaseZ;
  Vector2Int _inputCoordinatesTransformed;
  Vector2Int _outputCoordinatesTransformed;

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    if (_particleSystem != null) {
      _particlesRunner = _particlesRunnerFactory.CreateForFinishedState(GameObjectFast, _particleSystem);
    }
    UpdateAdjustableValuesFromPrefab();
    enabled = false;
  }

  void OnDestroy() {
    _directWaterServiceAccessor.DeleteWaterMover(_waterMover);
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

  void UpdateAdjustableValuesFromPrefab() {
    WaterFlow = _waterFlowPerSecond;
  }
  #endregion

  #region TickableComponent implemenatation
  /// <inheritdoc/>
  public override void StartTickable() {
    base.StartTickable();

    _valveBaseZ = _blockObject.Coordinates.z;
    _inputCoordinatesTransformed = _blockObject.Transform(_inputCoordinates);
    _outputCoordinatesTransformed = _blockObject.Transform(_outputCoordinates);
    _waterMover.InputTileIndex = _mapIndexService.CoordinatesToIndex(_inputCoordinatesTransformed);
    _waterMover.OutputTileIndex = _mapIndexService.CoordinatesToIndex(_outputCoordinatesTransformed);
    MinWaterLevelAtIntake = MinWaterLevelAtIntake;
    MaxWaterLevelAtOuttake = MaxWaterLevelAtOuttake;
    MoveContaminatedWater = MoveContaminatedWater;
    _directWaterServiceAccessor.AddWaterMover(_waterMover);
  }

  /// <inheritdoc/>
  public override void Tick() {
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
      UpdateAdjustableValuesFromPrefab();
      return;
    }
    var state = entityLoader.GetComponent(WaterValveKey);
    WaterFlow = state.GetValueOrNullable(WaterFlowLimitKey) ?? FlowLimit;
    OutputLevelCheckDisabled = state.GetValueOrNullable(OutputLevelCheckDisabledKey) ?? _outputLevelCheckDisabled;
    MoveContaminatedWater = state.GetValueOrNullable(MoveContaminatedWaterKey) ?? _moveContaminatedWater;
  }
  #endregion

  #region IFinishedStateListener implementation
  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    enabled = true;
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    enabled = false;
  }
  #endregion
}

}
