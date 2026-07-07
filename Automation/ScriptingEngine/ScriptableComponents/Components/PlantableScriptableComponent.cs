// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Forestry;
using Timberborn.Multithreading;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class PlantableScriptableComponent : ScriptableComponentBase, ITickableSingleton, IParallelTickableSingleton {

  const string SpotReadySignalLocKey = "IgorZ.Automation.Scriptable.Plantable.Signal.SpotsReady";
  
  const string SpotReadySignalName = "Plantable.Ready";

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    if (!_isParallelTickingStarted) {
      return;
    }
    _isParallelTickingStarted = false;

    // Get the state from the parallel jobs and fire the signals.
    for (var i = _allTrackers.Count - 1; i >= 0; i--) {
      _allTrackers[i].FinalizeParallelUpdateState();
    }
  }

  #endregion

  #region IParallelTickableSingleton implementation

  /// <inheritdoc/>
  public void StartParallelTick() {
    _isParallelTickingStarted = true;
    for (var i = _allTrackers.Count - 1; i >= 0; i--) {
      _parallelizer.Schedule(new UpdateTrackerJob(_allTrackers[i]));
    }
  }
  bool _isParallelTickingStarted;

  #endregion

  #region Parallel job implemenation

  /// <summary>
  /// This job assumes that many things are constant between the "regular ticks". Like water levels, contamination, etc.
  /// If not, we're in trouble.
  /// </summary>
  readonly struct UpdateTrackerJob : IParallelizerSingleTask {

    readonly PlantableTracker _tracker;

    /// <summary>
    /// This job assumes that many things are constant between the "regular ticks". Like water levels, contamination,
    /// etc. If not, we're in trouble.
    /// </summary>
    /// <param name="tracker">The tracker to update.</param>
    public UpdateTrackerJob(PlantableTracker tracker) {
      _tracker = tracker;
      _tracker.PrepareForParallelUpdateState();
    }

    /// <inheritdoc/>
    public void Run() {
      _tracker.ParallelUpdateState();
    }
  } 

  #endregion

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Plantable";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var plantingSpotFinder = GetPlantingSpotFinder(behavior, throwIfNotFound: false);
    if (!plantingSpotFinder) {
      return [];
    }
    return [SpotReadySignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    if (name != SpotReadySignalName) {
      throw new ScriptError.ParsingError("Unknown signal: " + name);
    }
    GetPlantingSpotFinder(behavior);
    return () => HasSpotsReadySignal(behavior);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    if (name != SpotReadySignalName) {
      throw new ScriptError.ParsingError("Unknown signal: " + name);
    }
    GetPlantingSpotFinder(behavior);
    return HasSpotsSignalDef;
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName != SpotReadySignalName) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    GetPlantingSpotFinder(host.Behavior);
    var tracker = host.Behavior.GetOrCreate<PlantableTracker>();
    if (tracker.AddSignal(signalOperator, host)) {
      _allTrackers.Add(tracker);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var tracker = host.Behavior.GetOrThrow<PlantableTracker>();
    if (!tracker.RemoveSignal(signalOperator, host)) {
      _allTrackers.Remove(tracker);
    }
  }

  #endregion

  #region Signals

  SignalDef HasSpotsSignalDef => _collectableReadySignalDef ??= new SignalDef {
      ScriptName = SpotReadySignalName,
      DisplayName = Loc.T(SpotReadySignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _collectableReadySignalDef;

  static ScriptValue HasSpotsReadySignal(AutomationBehavior behavior) {
    var tracker = behavior.GetOrCreate<PlantableTracker>();
    return ScriptValue.FromInt(tracker.SpotsForPlanting);
  }

  #endregion

  #region Implementation

  readonly IParallelizer _parallelizer;

  internal static PlantableScriptableComponent Instance;
  readonly List<PlantableTracker> _allTrackers = [];

  PlantableScriptableComponent(IParallelizer parallelizer) {
    _parallelizer = parallelizer;
    Instance = this;
  }

  static PlantingSpotFinder GetPlantingSpotFinder(AutomationBehavior behavior, bool throwIfNotFound = true) {
    var plantingCoordinates = behavior.GetComponent<InRangePlantingCoordinates>();
    var plantingSpotFinder = behavior.GetComponent<PlantingSpotFinder>();
    if (plantingCoordinates && plantingSpotFinder) {
      return plantingSpotFinder;
    }
    if (throwIfNotFound) {
      throw new ScriptError.BadStateError(behavior, "Building cannot plant crops or trees");
    }
    return null;
  }

  #endregion

  #region Plantable coordiantes spots tracker component

  internal sealed class PlantableTracker : AbstractStatusTracker, IAwakableComponent, IFinishedStateListener {

    #region IFinishedStateListener implementation

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      _eventBus.Register(this);
      _buildingTerrainRange.RangeChanged += BuildingRangeChanged;
      ScheduleStateUpdate();
    }

    /// <inheritdoc/>
    public void OnExitFinishedState() {
      _eventBus.Unregister(this);
      _buildingTerrainRange.RangeChanged -= BuildingRangeChanged;
    }

    #endregion

    #region API

    /// <summary>Returns the number of tiles that offer collectable items.</summary>
    public int SpotsForPlanting {
      get => _spotsForPlanting;
      private set {
        _spotsForPlanting = value;
        TriggerSignalUpdate(SpotReadySignalName);
      }
    }
    int _spotsForPlanting;

    /// <summary>Prepares the state for calculation in a parallel thread.</summary>
    /// <remarks>
    /// All data that can change should be copied before calling <see cref="ParallelUpdateState"/>. This method is
    /// called from <see cref="IParallelTickableSingleton"/>.
    /// </remarks>
    public void PrepareForParallelUpdateState() {
      _threadsafePlantingSpots = _plantingCoordinates.GetCoordinates()._set.ToArray();
    }
    Vector3Int[] _threadsafePlantingSpots;

    /// <summary>Calculates the state in a parallel thread.</summary>
    /// <remarks>
    /// <p>
    /// This method is called in a parallel thread to calculate the number of plantable spots. It must not access any
    /// data that can change between the ticks. The data that can potentially change should be copied in
    /// <see cref="PrepareForParallelUpdateState"/>.
    /// </p>
    /// <p>
    /// The result will not be visible until the calculation is finalized via <see cref="FinalizeParallelUpdateState"/>.
    /// </p>
    /// </remarks>
    /// <seealso cref="FinalizeParallelUpdateState"/>
    public void ParallelUpdateState() {
      _parallelCountedSpotsForPlanting = CalculatePlantableSpots(_threadsafePlantingSpots);
      _threadsafePlantingSpots = null;
    }
    int _parallelCountedSpotsForPlanting;

    /// <summary>
    /// Applies the state calculated in the parallel update to the main thread and fires signals if needed.
    /// </summary>
    /// <remarks>
    /// This should be called to apply the result from <see cref="ParallelUpdateState"/>. A normal place to do that is
    /// the singleton tick callback.
    /// </remarks>
    /// <seealso cref="ParallelUpdateState"/>
    public void FinalizeParallelUpdateState() {
      SpotsForPlanting = _parallelCountedSpotsForPlanting;
    }

    #endregion

    #region AbstractStatusTracker overrides

    /// <inheritdoc/>
    public override void OnPostLoadActivation() {
      base.OnPostLoadActivation();
      UpdateState();
    }

    #endregion

    #region Implementation

    EventBus _eventBus;
    PlantingService _plantingService;

    PlantingSpotFinder _plantingSpotFinder;
    BuildingTerrainRange _buildingTerrainRange;
    InRangePlantingCoordinates _plantingCoordinates;

    [Inject]
    public void InjectDependencies(EventBus eventBus, PlantingService plantingService) {
      _eventBus = eventBus;
      _plantingService = plantingService;
    }

    public void Awake() {
      _plantingSpotFinder = AutomationBehavior.GetComponentOrFail<PlantingSpotFinder>();
      _buildingTerrainRange = AutomationBehavior.GetComponentOrFail<BuildingTerrainRange>();
      _plantingCoordinates = AutomationBehavior.GetComponentOrFail<InRangePlantingCoordinates>();
    }

    void UpdateState() {
      SpotsForPlanting = CalculatePlantableSpots(_plantingCoordinates.GetCoordinates()._set.ToArray());
    }

    void ScheduleStateUpdate() {
      if (!AutomationService.AutomationSystemReady) {
        return; // The game is loading. Wait for OnRulesBound().
      }
      if (Time.timeScale != 0f) {
        return; // The state will be updated in parallel tick.
      }
      if (_updateCoroutine != null) {
        return; // Already scheduled.
      }
      _updateCoroutine = MonoBehaviour.StartCoroutine(UpdateCoroutine());
    }
    Coroutine _updateCoroutine;

    IEnumerator UpdateCoroutine() {
      yield return new WaitForEndOfFrame();
      _updateCoroutine = null;
      UpdateState();
    }

    int CalculatePlantableSpots(Vector3Int[] plantingSpots) {
      var plantableSpots = 0;
      foreach (var coords in plantingSpots) {
        var plantingSpot = _plantingService.GetSpotAt(coords);
        if (!plantingSpot.HasValue && _plantingService._reservedCoordinates.Contains(coords)) {
          // When worker reserves planting coords, the spot is not returning from GetSpotAt.
          var resourceAt = _plantingService.GetResourceAt(coords);
          if (resourceAt != null) {
            plantingSpot = _plantingService.CreatePlantingSpot(coords, resourceAt);
          }
        }
        if (plantingSpot.HasValue && _plantingSpotFinder.CanPlantAt(plantingSpot.Value, null)) {
          plantableSpots++;
        }
      }
      return plantableSpots;
    }

    #endregion

    #region Callbacks for the pasue mode

    /// <summary>Monitors for changes in the building-reachable area.</summary>
    void BuildingRangeChanged(object sender, RangeChangedEventArgs args) {
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesSet(PlantingCoordinatesSetEvent plantingCoordinatesSetEvent) {
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesUnset(PlantingCoordinatesUnsetEvent plantingCoordinatesUnsetEvent) {
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for changes in the tree cutting area (the "replant dead trees" case).</summary>
    /// <remarks>The dead plants only eligible for re-planting if not marked for cutting.</remarks>
    [OnEvent]
    public void OnTreeCuttingAreaChangedEvent(TreeCuttingAreaChangedEvent e) {
      ScheduleStateUpdate();
    }

    #endregion
  }

  #endregion
}
