// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.Forestry;
using Timberborn.Multithreading;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class PlantableScriptableComponent : ScriptableComponentBase, ITickableSingleton, IParallelTickableSingleton {

  const string SpotReadySignalLocKey = "IgorZ.Automation.Scriptable.Plantable.Signal.SpotsReady";
  
  const string SpotReadySignalName = "Plantable.Ready";

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    // Get the state from the parallel jobs and fire the signals.
    for (var i = _allTrackers.Count - 1; i >= 0; i--) {
      _allTrackers[i].FinalizeParallelUpdateState();
    }
  }

  #endregion

  #region IParallelTickableSingleton implementation

  /// <inheritdoc/>
  public void StartParallelTick() {
    var task = new UpdateTrackerJob(_allTrackers);
    _parallelizer.Schedule(0, _allTrackers.Count, 10, task);
  }

  #endregion

  #region Parallel job implemenation

  /// <summary>
  /// This job assumes that many things are constant between the "regular ticks". Like water levels, contamination, etc.
  /// If not, we're in troubles.
  /// </summary>
  /// <param name="allTrackers">The trackers to update.</param>
  readonly struct UpdateTrackerJob(IList<PlantableTracker> allTrackers) : IParallelizerLoopTask {
    readonly ReadOnlyArray<PlantableTracker> _allTrackers = new(allTrackers.ToArray());

    /// <inheritdoc/>
    public void Run(int index) {
      _allTrackers[index].ParallelUpdateState();
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
    var hadSignals = tracker.HasSignals;
    tracker.AddSignal(signalOperator, host);
    if (!hadSignals && tracker.HasSignals) {
      _allTrackers.Add(tracker);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var tracker = host.Behavior.GetOrThrow<PlantableTracker>();
    tracker.RemoveSignal(signalOperator, host);
    if (!tracker.HasSignals) {
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
          ValueValidator = ValueDef.RangeCheckValidatorInt(0),
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
    var plantingCoordinates = behavior.GetComponentFast<InRangePlantingCoordinates>();
    var plantingSpotFinder = behavior.GetComponentFast<PlantingSpotFinder>();
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

  sealed class PlantableTracker : AbstractStatusTracker, IFinishedStateListener {

    #region IFinishedStateListener implementation

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      _eventBus.Register(this);
      _buildingTerrainRange.RangeChanged += BuildingRangeChanged;
      ImmediateUpdateState();
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
        if (_spotsForPlanting == value) {
          return;
        }
        _spotsForPlanting = value;
        ScheduleSignal(SpotReadySignalName, ignoreErrors: true);
      }
    }

    int _spotsForPlanting;

    /// <summary>Calculates the state in a parallel thread.</summary>
    /// <remarks>
    /// <p>
    /// This method is called in a parallel thread to calculate the number of plantable spots. It must not access any
    /// data that can change between the ticks.
    /// </p>
    /// <p>
    /// The result will not be visible until the calculation is finalized via <see cref="FinalizeParallelUpdateState"/>.
    /// </p>
    /// </remarks>
    /// <seealso cref="FinalizeParallelUpdateState"/>
    public void ParallelUpdateState() {
      _parallelCountedSpotsForPlanting = CalculatePlantableSpots(_plantingCoordinates.GetCoordinates()._set.ToArray());
    }
    int _parallelCountedSpotsForPlanting;

    /// <summary>
    /// Applies the state calculated in the parallel update to the main thread and fires signals if needed.
    /// </summary>
    /// <remarks>
    /// This should be called to apply the result from <see cref="ParallelUpdateState"/>. A normal place to do that, is
    /// the singleton tick callback.
    /// </remarks>
    /// <seealso cref="ParallelUpdateState"/>
    public void FinalizeParallelUpdateState() {
      SpotsForPlanting = _parallelCountedSpotsForPlanting;
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

      // This component is added dynamically, so it won't get the finished state callback on a finished building.
      if (GetComponentFast<BlockObject>().IsFinished) {
        OnEnterFinishedState();
      }
    }

    void Awake() {
      _plantingSpotFinder = GetComponentFast<PlantingSpotFinder>();
      _buildingTerrainRange = GetComponentFast<BuildingTerrainRange>();
      _plantingCoordinates = GetComponentFast<InRangePlantingCoordinates>();
    }

    void ImmediateUpdateState() {
      if (Time.timeScale != 0f) {
        return; // The state will be updated in parallel tick.
      }
      if (_immediateUpdateCoroutine != null) {
        return; // Already scheduled.
      }
      _immediateUpdateCoroutine = StartCoroutine(ImmediateUpdateCoroutine());
    }
    Coroutine _immediateUpdateCoroutine;

    IEnumerator ImmediateUpdateCoroutine() {
      yield return new WaitForEndOfFrame();
      _immediateUpdateCoroutine = null;
      ParallelUpdateState();
      FinalizeParallelUpdateState();
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
      ImmediateUpdateState();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesSet(PlantingCoordinatesSetEvent plantingCoordinatesSetEvent) {
      ImmediateUpdateState();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesUnset(PlantingCoordinatesUnsetEvent plantingCoordinatesUnsetEvent) {
      ImmediateUpdateState();
    }

    /// <summary>Monitors for changes in the tree cutting area (the "replant dead trees" case).</summary>
    /// <remarks>The dead plants only eligible for re-planting if not marked for cutting.</remarks>
    [OnEvent]
    public void OnTreeCuttingAreaChangedEvent(TreeCuttingAreaChangedEvent e) {
      ImmediateUpdateState();
    }

    #endregion
  }

  #endregion
}
