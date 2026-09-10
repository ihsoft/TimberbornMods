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
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Multithreading;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class PlantableScriptableComponent : ScriptableComponentBase, ITickableSingleton, IParallelTickableSingleton {

  const string SpotReadySignalLocKey = "IgorZ.Automation.Scriptable.Plantable.Signal.SpotsReady";
  const string ResourceReadySignalLocKey = "IgorZ.Automation.Scriptable.Plantable.Signal.ResourceReady";
  const string SetPriorityActionLocKey = "IgorZ.Automation.Scriptable.Plantable.Action.SetPriority";
  const string NoPriorityLocKey = "Planting.NoPriorityOption";
  
  const string SpotReadySignalName = "Plantable.Ready";
  const string SetPriorityActionName = "Plantable.SetPriority";

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
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<PlantablePrioritizer>() && behavior.GetComponent<PlanterBuilding>()
        ? [SetPriorityActionName] : [];
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    if (name != SetPriorityActionName) {
      throw new UnknownActionException(name);
    }
    GetComponentOrThrow<PlantablePrioritizer>(behavior);
    var planter = GetComponentOrThrow<PlanterBuilding>(behavior);
    return new ActionDef {
        ScriptName = name,
        DisplayName = Loc.T(SetPriorityActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.String,
                Options = [
                    new DropdownItem { Value = "", Text = Loc.T(NoPriorityLocKey) },
                    ..planter.AllowedPlantables.Select(x => new DropdownItem {
                        Value = x.TemplateName,
                        Text = Loc.T(x.GetSpec<LabeledEntitySpec>().DisplayNameLocKey),
                    }),
                ],
            },
        ],
    };
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    if (name != SetPriorityActionName) {
      throw new UnknownActionException(name);
    }
    var prioritizer = GetComponentOrThrow<PlantablePrioritizer>(behavior);
    var planter = GetComponentOrThrow<PlanterBuilding>(behavior);
    return args => SetPriorityAction(args, prioritizer, planter);
  }

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var plantingSpotFinder = GetPlantingSpotFinder(behavior, throwIfNotFound: false);
    if (!plantingSpotFinder) {
      return [];
    }
    var planter = behavior.GetComponent<PlanterBuilding>();
    return planter
        ? [SpotReadySignalName, ..planter.AllowedPlantables.Select(x => MakeResourceSignalName(x.TemplateName))]
        : [SpotReadySignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var templateName = ResolveSignalPlantable(name, behavior)?.TemplateName;
    return () => ScriptValue.FromInt(behavior.GetOrCreate<PlantableTracker>().GetReadySpots(templateName));
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var plantable = ResolveSignalPlantable(name, behavior);
    if (plantable == null) {
      return SpotsReadySignalDef;
    }
    var plantableName = Loc.T(plantable.GetSpec<LabeledEntitySpec>().DisplayNameLocKey);
    return new SignalDef {
        ScriptName = name,
        DisplayName = Loc.T(ResourceReadySignalLocKey, plantableName),
        DisplayNameLocKey = ResourceReadySignalLocKey,
        DisplayNameArgument = plantableName,
        Scope = SignalDef.ScopeEnum.Building,
        Result = SpotsReadySignalDef.Result,
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    ResolveSignalPlantable(signalOperator.SignalName, host.Behavior);
    host.Behavior.GetOrCreate<PlantableTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<PlantableTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Actions

  static void SetPriorityAction(ScriptValue[] args, PlantablePrioritizer prioritizer, PlanterBuilding planter) {
    AssertActionArgsCount(SetPriorityActionName, args, 1);
    var templateName = args[0].AsString;
    var plantable = templateName == ""
        ? null : planter.AllowedPlantables.SingleOrDefault(x => x.TemplateName == templateName);
    if (templateName != "" && plantable == null) {
      throw new ScriptError.BadValue("Unsupported plantable: " + templateName);
    }
    // Farms use this same priority for both planting and harvesting.
    if (prioritizer.PrioritizedPlantableSpec != plantable) {
      prioritizer.PrioritizePlantable(plantable);
    }
  }

  #endregion

  #region Signals

  SignalDef SpotsReadySignalDef => _spotsReadySignalDef ??= new SignalDef {
      ScriptName = SpotReadySignalName,
      DisplayName = Loc.T(SpotReadySignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _spotsReadySignalDef;

  /// <summary>Builds <c>Plantable.Ready.{encodedTemplate}</c> using <see cref="SignalNameSegment.Encode"/>.</summary>
  /// <remarks>
  /// Safe names remain raw; other names use the shared codec. The legacy <c>Plantable.Ready</c> total is unchanged.
  /// </remarks>
  static string MakeResourceSignalName(string templateName) {
    return SpotReadySignalName + "." + SignalNameSegment.Encode(templateName);
  }

  /// <summary>Resolves a generated plant signal to its spec, or the legacy total signal to null.</summary>
  /// <remarks>Names that do not exactly match an allowed plant's encoded signal name raise a parsing error.</remarks>
  static PlantableSpec ResolveSignalPlantable(string name, AutomationBehavior behavior) {
    GetPlantingSpotFinder(behavior);
    if (name == SpotReadySignalName) {
      return null;
    }
    var planter = GetComponentOrThrow<PlanterBuilding>(behavior);
    return planter.AllowedPlantables.FirstOrDefault(x => MakeResourceSignalName(x.TemplateName) == name)
        ?? throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  #endregion

  #region Implementation

  readonly IParallelizer _parallelizer;

  static PlantableScriptableComponent Instance;
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

    /// <summary>Returns the total number of ready planting spots.</summary>
    public int SpotsForPlanting { get; private set; }

    /// <summary>Returns ready spots for a plantable template, or all ready spots when the name is null.</summary>
    public int GetReadySpots(string templateName) {
      return templateName == null ? SpotsForPlanting : _spotsByPlantable.GetValueOrDefault(templateName);
    }

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
    Dictionary<string, int> _parallelCountedSpotsForPlanting;

    /// <summary>
    /// Applies the state calculated in the parallel update to the main thread and fires signals if needed.
    /// </summary>
    /// <remarks>
    /// This should be called to apply the result from <see cref="ParallelUpdateState"/>. A normal place to do that is
    /// the singleton tick callback.
    /// </remarks>
    /// <seealso cref="ParallelUpdateState"/>
    public void FinalizeParallelUpdateState() {
      if (_parallelCountedSpotsForPlanting == null) {
        return;
      }
      var countedSpots = _parallelCountedSpotsForPlanting;
      _parallelCountedSpotsForPlanting = null;
      ApplyPlantableSpots(countedSpots);
    }

    #endregion

    #region AbstractStatusTracker overrides

    /// <inheritdoc/>
    public override void OnPostLoadActivation() {
      base.OnPostLoadActivation();
      UpdateState();
    }

    /// <inheritdoc/>
    public override bool AddSignal(SignalOperator signalOperator, ISignalListener host) {
      var firstListener = base.AddSignal(signalOperator, host);
      if (firstListener && _trackedSignals.Add(signalOperator.SignalName) && _trackedSignals.Count == 1) {
        Instance._allTrackers.Add(this);
      }
      return firstListener;
    }

    /// <inheritdoc/>
    public override bool RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
      var hasListeners = base.RemoveSignal(signalOperator, host);
      if (!hasListeners) {
        _trackedSignals.Remove(signalOperator.SignalName);
        if (_trackedSignals.Count == 0) {
          Instance._allTrackers.Remove(this);
        }
      }
      return hasListeners;
    }

    #endregion

    #region Implementation

    EventBus _eventBus;
    PlantingService _plantingService;

    PlantingSpotFinder _plantingSpotFinder;
    BuildingTerrainRange _buildingTerrainRange;
    InRangePlantingCoordinates _plantingCoordinates;
    readonly HashSet<string> _trackedSignals = [];
    Dictionary<string, int> _spotsByPlantable = [];

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
      ApplyPlantableSpots(CalculatePlantableSpots(_plantingCoordinates.GetCoordinates()._set.ToArray()));
    }

    void ApplyPlantableSpots(Dictionary<string, int> spotsByPlantable) {
      _spotsByPlantable = spotsByPlantable;
      SpotsForPlanting = spotsByPlantable.Values.Sum();
      foreach (var signalName in _trackedSignals) {
        TriggerSignalUpdate(signalName);
      }
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

    Dictionary<string, int> CalculatePlantableSpots(Vector3Int[] plantingSpots) {
      var plantableSpots = new Dictionary<string, int>();
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
          var templateName = plantingSpot.Value.ResourceToPlant;
          plantableSpots[templateName] = plantableSpots.GetValueOrDefault(templateName) + 1;
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
