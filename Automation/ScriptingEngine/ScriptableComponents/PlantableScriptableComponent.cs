// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class PlantableScriptableComponent : ScriptableComponentBase, ITickableSingleton {

  const string SpotReadySignalLocKey = "IgorZ.Automation.Scriptable.Plantable.Signal.SpotsReady";
  
  const string SpotReadySignalName = "Plantable.Ready";

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

  internal static PlantableScriptableComponent Instance;
  static readonly Stopwatch CallbacksCostWatch = new();
  static readonly Stopwatch UpdateCostWatch = new();
  readonly List<PlantableTracker> _allTrackers = [];

  PlantableScriptableComponent() {
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

  void UpdateAffectedTrackers(Vector3Int coordinates) {
    for (var i = _allTrackers.Count - 1; i >= 0; i--) {
      _allTrackers[i].UpdateForPlantingSpot(coordinates);
    }
  }

  #endregion

  #region Callbacks

  internal void OnForesterSettingsChanged(Forester forester) {
    CallbacksCostWatch.Start();
    var tracker = forester.GetComponentFast<PlantableTracker>();
    if (tracker) {
      tracker.ScheduleStateUpdate();
    }
    CallbacksCostWatch.Stop();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void OnMoistureChanged(Vector3Int coordinates) {
    //FIXME: maintain a big bitmap of all spots from all components.
    CallbacksCostWatch.Start();
    UpdateAffectedTrackers(coordinates);
    CallbacksCostWatch.Stop();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void OnContaminationChanged(Vector3Int coordinates) {
    CallbacksCostWatch.Start();
    UpdateAffectedTrackers(coordinates);
    CallbacksCostWatch.Stop();
  }

  #endregion

  #region Inventory change tracker component

  sealed class PlantableTracker : AbstractStatusTracker, IFinishedStateListener {

    #region IFinishedStateListener implementation

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      _eventBus.Register(this);
      _buildingTerrainRange.RangeChanged += BuildingRangeChanged;
      UpdateState();
    }

    /// <inheritdoc/>
    public void OnExitFinishedState() {
      _eventBus.Unregister(this);
      _buildingTerrainRange.RangeChanged -= BuildingRangeChanged;
    }

    #endregion

    #region API

    /// <summary>Returns the number of tiles that offer collectable items.</summary>
    public int SpotsForPlanting => _spotsForPlanting;

    /// <summary>Updates the component if the given coordinates are its planting spot.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateForPlantingSpot(Vector3Int coordinates) {
      if (_buildingTerrainRange.GetRange().Contains(coordinates)) {
        ScheduleStateUpdate();
      }
    }

    /// <summary>Schedules a state update at the end of the frame.</summary>
    public void ScheduleStateUpdate() {
      _stateUpdateCoroutine ??= StartCoroutine(StateUpdateCoroutine());
    }
    Coroutine _stateUpdateCoroutine;

    #endregion

    #region Implementation

    EventBus _eventBus;
    PlantingService _plantingService;

    PlantingSpotFinder _plantingSpotFinder;
    InRangePlantingCoordinates _coordinatesInRange;
    BuildingTerrainRange _buildingTerrainRange;

    int _spotsForPlanting;

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
      _coordinatesInRange = GetComponentFast<InRangePlantingCoordinates>();
      _buildingTerrainRange = GetComponentFast<BuildingTerrainRange>();
    }

    void UpdateForBlockObject(BaseComponent component) {
      var blockObject = component.GetComponentFast<BlockObject>();
      if (blockObject) {
        UpdateForPlantingSpot(blockObject.Coordinates);
      }
    }

    IEnumerator StateUpdateCoroutine() {
      yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure all changes are processed.
      UpdateCostWatch.Start();
      UpdateState();
      _stateUpdateCoroutine = null;
      UpdateCostWatch.Stop();
    }

    void UpdateState() {
      var oldState = _spotsForPlanting;
      _spotsForPlanting = CountPlantingSpotsInRange();
      if (oldState == _spotsForPlanting) {
        return; // No change in the state.
      }
      ScheduleSignal(SpotReadySignalName, ignoreErrors: true);
    }

    int CountPlantingSpotsInRange() {
      var count = 0;
      var plantableCoordinates = _coordinatesInRange.GetCoordinates();
      foreach (var coords in plantableCoordinates) {
        var plantingSpot = _plantingService.GetSpotAt(coords);
        if (!plantingSpot.HasValue && _plantingService._reservedCoordinates.Contains(coords)) {
          // When worker reserves planting coords, the spot is not returning from GetSpotAt.
          var resourceAt = _plantingService.GetResourceAt(coords);
          if (resourceAt != null) {
            plantingSpot = _plantingService.CreatePlantingSpot(coords, resourceAt);
          }
        }
        if (plantingSpot.HasValue && _plantingSpotFinder.CanPlantAt(plantingSpot.Value, null)) {
          count++;
        }
      }
      HostedDebugLog.Fine(
          this, "Updated spots for planting: total={0}, plantable={1}", plantableCoordinates.Count, count);
      return count;
    }

    #endregion

    #region Callbacks

    /// <summary>Monitors for changes in the building-reachable area.</summary>
    /// <remarks>It is handled in ticks, so it doesn't happen on pause.</remarks>
    void BuildingRangeChanged(object sender, RangeChangedEventArgs args) {
      CallbacksCostWatch.Start();
      ScheduleStateUpdate();
      CallbacksCostWatch.Stop();
    }

    /// <summary>Monitors for the spots being taken by a grown resource.</summary>
    [OnEvent]
    public void OnEntityInitializedEvent(EntityInitializedEvent e) {
      CallbacksCostWatch.Start();
      UpdateForBlockObject(e.Entity);
      CallbacksCostWatch.Stop();
    }

    /// <summary>Monitors for the new spots when the resources get gathered.</summary>
    [OnEvent]
    public void OnEntityDeletedEvent(EntityDeletedEvent e) {
      CallbacksCostWatch.Start();
      UpdateForBlockObject(e.Entity);
      CallbacksCostWatch.Stop();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesSet(PlantingCoordinatesSetEvent plantingCoordinatesSetEvent) {
      CallbacksCostWatch.Start();
      if (_buildingTerrainRange.GetRange().Contains(plantingCoordinatesSetEvent.Coordinates)) {
        ScheduleStateUpdate();
      }
      CallbacksCostWatch.Stop();
    }

    /// <summary>Monitors for changes in the crop/tree planting area.</summary>
    [OnEvent]
    public void OnPlantingCoordinatesUnset(PlantingCoordinatesUnsetEvent plantingCoordinatesUnsetEvent) {
      CallbacksCostWatch.Start();
      if (_buildingTerrainRange.GetRange().Contains(plantingCoordinatesUnsetEvent.Coordinates)) {
        ScheduleStateUpdate();
      }
      CallbacksCostWatch.Stop();
    }

    /// <summary>Monitors for changes in the tree cutting area (the "replant dead trees" case).</summary>
    [OnEvent]
    public void OnTreeCuttingAreaChangedEvent(TreeCuttingAreaChangedEvent e) {
      CallbacksCostWatch.Start();
      ScheduleStateUpdate();
      CallbacksCostWatch.Stop();
    }

    #endregion
  }

  #endregion

  //FIXME: remove when profiling is done.
  public void Tick() {
    DebugEx.Warning("*** Tracking costs: callbacks={0}, recalc={1}", CallbacksCostWatch.Elapsed, UpdateCostWatch.Elapsed);
    UpdateCostWatch.Reset();
    CallbacksCostWatch.Reset();
  }
}
