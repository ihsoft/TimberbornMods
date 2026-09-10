// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.GoodStackSystem;
using Timberborn.Growing;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.Ruins;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.YielderFinding;
using Timberborn.Yielding;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class CollectableScriptableComponent : ScriptableComponentBase {

  const string CollectableReadySignalLocKey = "IgorZ.Automation.Scriptable.Collectable.Signal.CollectableReady";
  const string GoodReadySignalLocKey = "IgorZ.Automation.Scriptable.Collectable.Signal.GoodReady";
  const string SetPriorityActionLocKey = "IgorZ.Automation.Scriptable.Collectable.Action.SetPriority";
  const string NoPriorityLocKey = "Gathering.Nothing";

  const string CollectableReadySignalName = "Collectable.Ready";
  const string SetPriorityActionName = "Collectable.SetPriority";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Collectable";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<GatherablePrioritizer>() && behavior.GetComponent<GathererFlag>()
        ? [SetPriorityActionName] : [];
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    if (name != SetPriorityActionName) {
      throw new UnknownActionException(name);
    }
    GetComponentOrThrow<GatherablePrioritizer>(behavior);
    var gatherer = GetComponentOrThrow<GathererFlag>(behavior);
    return new ActionDef {
        ScriptName = name,
        DisplayName = Loc.T(SetPriorityActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.String,
                Options = [
                    new DropdownItem { Value = "", Text = Loc.T(NoPriorityLocKey) },
                    ..gatherer.AllowedGatherables.Select(x => new DropdownItem {
                        Value = x.GetSpec<TemplateSpec>().TemplateName,
                        Text = _goodService.GetGood(x.Yielder.Yield.Id).PluralDisplayName.Value,
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
    var prioritizer = GetComponentOrThrow<GatherablePrioritizer>(behavior);
    var gatherer = GetComponentOrThrow<GathererFlag>(behavior);
    return args => SetPriorityAction(args, prioritizer, gatherer);
  }

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var yieldRemovingBuilding = GetYieldRemovingBuilding(behavior, throwIfNotFound: false);
    if (!yieldRemovingBuilding) {
      return [];
    }
    return [CollectableReadySignalName, ..yieldRemovingBuilding.GetAllowedGoods().Select(MakeGoodSignalName)];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var goodId = ResolveSignalGood(name, behavior);
    return () => ScriptValue.FromInt(behavior.GetOrCreate<GatherableTracker>().GetReadyYielders(goodId));
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var goodId = ResolveSignalGood(name, behavior);
    if (goodId == null) {
      return CollectableReadySignalDef;
    }
    var goodName = _goodService.GetGood(goodId).PluralDisplayName.Value;
    return new SignalDef {
        ScriptName = name,
        DisplayName = Loc.T(GoodReadySignalLocKey, goodName),
        DisplayNameLocKey = GoodReadySignalLocKey,
        DisplayNameArgument = goodName,
        Scope = SignalDef.ScopeEnum.Building,
        Result = CollectableReadySignalDef.Result,
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    ResolveSignalGood(signalOperator.SignalName, host.Behavior);
    host.Behavior.GetOrCreate<GatherableTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<GatherableTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Actions

  static void SetPriorityAction(ScriptValue[] args, GatherablePrioritizer prioritizer, GathererFlag gatherer) {
    AssertActionArgsCount(SetPriorityActionName, args, 1);
    var templateName = args[0].AsString;
    var gatherable = templateName == "" ? null : gatherer.AllowedGatherables.SingleOrDefault(
        x => x.GetSpec<TemplateSpec>().TemplateName == templateName);
    if (templateName != "" && gatherable == null) {
      throw new ScriptError.BadValue("Unsupported gatherable: " + templateName);
    }
    if (prioritizer.PrioritizedGatherable != gatherable) {
      prioritizer.PrioritizeGatherable(gatherable);
    }
  }

  #endregion

  #region Signals

  SignalDef CollectableReadySignalDef => _collectableReadySignalDef ??= new SignalDef {
      ScriptName = CollectableReadySignalName,
      DisplayName = Loc.T(CollectableReadySignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _collectableReadySignalDef;

  /// <summary>Builds <c>Collectable.Ready.{encodedGoodId}</c> using <see cref="SignalNameSegment.Encode"/>.</summary>
  /// <remarks>
  /// Safe IDs remain raw; other IDs use the shared codec. The legacy <c>Collectable.Ready</c> total is unchanged.
  /// </remarks>
  static string MakeGoodSignalName(string goodId) {
    return CollectableReadySignalName + "." + SignalNameSegment.Encode(goodId);
  }

  /// <summary>Resolves a generated good signal to its ID, or the legacy total signal to null.</summary>
  /// <remarks>Names that do not exactly match an allowed good's encoded signal name raise a parsing error.</remarks>
  static string ResolveSignalGood(string name, AutomationBehavior behavior) {
    var building = GetYieldRemovingBuilding(behavior);
    if (name == CollectableReadySignalName) {
      return null;
    }
    return building.GetAllowedGoods().FirstOrDefault(x => MakeGoodSignalName(x) == name)
        ?? throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  #endregion

  #region Implementation

  readonly IGoodService _goodService;

  CollectableScriptableComponent(IGoodService goodService) {
    _goodService = goodService;
  }

  static YieldRemovingBuilding GetYieldRemovingBuilding(AutomationBehavior behavior, bool throwIfNotFound = true) {
    var yieldRemovingBuilding = behavior.GetComponent<YieldRemovingBuilding>();
    var buildingTerrainRange = behavior.GetComponent<BuildingTerrainRange>();
    if (yieldRemovingBuilding && buildingTerrainRange) {
      return yieldRemovingBuilding;
    }
    if (throwIfNotFound) {
      throw new ScriptError.BadStateError(behavior, "Building cannot collect items");
    }
    return null;
  }

  #endregion

  #region Component that tracks gatherable items in the building area.

  internal sealed class GatherableTracker : AbstractStatusTracker, IAwakableComponent, IFinishedStateListener {

    #region IFinishedStateListener implementation

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      _eventBus.Register(this);
      _buildingTerrainRange.RangeChanged += BuildingRangeChanged;
      _rangeChanged = true;
      UpdateState();  // Sync-up the initial state.
    }

    /// <inheritdoc/>
    public void OnExitFinishedState() {
      _eventBus.Unregister(this);
      _buildingTerrainRange.RangeChanged -= BuildingRangeChanged;
      while (_yielders.Count > 0) {
        RemoveYielder(_yielders.First());
      }
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
      _trackedSignals.Add(signalOperator.SignalName);
      return firstListener;
    }

    /// <inheritdoc/>
    public override bool RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
      var hasListeners = base.RemoveSignal(signalOperator, host);
      if (!hasListeners) {
        _trackedSignals.Remove(signalOperator.SignalName);
      }
      return hasListeners;
    }

    #endregion

    #region API

    /// <summary>Returns the number of tiles that offer collectable items.</summary>
    public int ReadyYielders => _activeYielders;

    /// <summary>Returns ready objects yielding the given good, or all ready objects when the ID is null.</summary>
    public int GetReadyYielders(string goodId) {
      return goodId == null ? ReadyYielders : _readyYieldersByGood.GetValueOrDefault(goodId);
    }

    #endregion

    #region Implementation

    EventBus _eventBus;
    BlockService _blockService;
    BuildingTerrainRange _buildingTerrainRange;
    YieldRemovingBuilding _yieldRemovingBuilding;
    TreeCuttingArea _treeCuttingArea;

    readonly HashSet<Yielder> _yielders = [];
    readonly HashSet<string> _trackedSignals = [];
    readonly Dictionary<string, int> _readyYieldersByGood = [];
    int _activeYielders;
    bool _yieldersChanged;
    bool _rangeChanged;
    bool _needsCuttingArea;
    bool _noAliveCheck;

    [Inject]
    public void InjectDependencies(EventBus eventBus, BlockService blockService, TreeCuttingArea treeCuttingArea) {
      _eventBus = eventBus;
      _blockService = blockService;
      _treeCuttingArea = treeCuttingArea;
    }

    public void Awake() {
      _buildingTerrainRange = AutomationBehavior.GetComponentOrFail<BuildingTerrainRange>();
      _yieldRemovingBuilding = AutomationBehavior.GetComponentOrFail<YieldRemovingBuilding>();
      _needsCuttingArea = AutomationBehavior.GetComponent<LumberjackFlagWorkplaceBehavior>();
      _noAliveCheck = _needsCuttingArea || AutomationBehavior.GetComponent<ScavengerWorkplaceBehavior>();
    }

    void ScheduleStateUpdate() {
      if (!AutomationService.AutomationSystemReady) {
        return; // The game is loading. Wait for OnRulesBound().
      }
      _stateUpdateCoroutine ??= MonoBehaviour.StartCoroutine(StateUpdateCoroutine());
    }
    Coroutine _stateUpdateCoroutine;

    IEnumerator StateUpdateCoroutine() {
      yield return new WaitForEndOfFrame(); // Wait for the end of the frame to ensure all changes are processed.
      UpdateState();
      _stateUpdateCoroutine = null;
    }

    void UpdateState() {
      if (_rangeChanged) {
        _rangeChanged = false;
        UpdateArea();
      }
      if (!_yieldersChanged) {
        return;
      }
      _yieldersChanged = false;

      HostedDebugLog.Fine(AutomationBehavior, "Recalculating {0} yielders", _yielders.Count);
      _activeYielders = 0;
      _readyYieldersByGood.Clear();
      for (var i = _yielders.Count - 1; i >= 0; i--) {
        var yielder = _yielders.ElementAt(i);
        if (yielder.IsYielding && (_noAliveCheck || yielder.IsAlive())) {
          CountReadyYielder(yielder);
          continue;
        }
        // If a good stack is created, it consumes the whole yielder content.
        var goodStack = yielder.GetComponent<GoodStack>();
        if (!goodStack) {
          continue;
        }
        goodStack.GoodStackDisabled -= OnGoodStackDisabled;
        var hasGood = goodStack.Inventory.Enabled && !goodStack.Inventory.IsEmpty;
        if (hasGood) {
          CountReadyYielder(yielder);
          goodStack.GoodStackDisabled += OnGoodStackDisabled;
        }
      }
      foreach (var signalName in _trackedSignals) {
        TriggerSignalUpdate(signalName);
      }
    }

    void CountReadyYielder(Yielder yielder) {
      ++_activeYielders;
      var goodId = yielder.YielderSpec.Yield.Id;
      _readyYieldersByGood[goodId] = _readyYieldersByGood.GetValueOrDefault(goodId) + 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddYielder(Yielder yielder) {
      if (!_yielders.Add(yielder)) {
        HostedDebugLog.Error(AutomationBehavior, "Yielder already exists in the set: {0}", yielder);
        return;
      }
      yielder.YieldDecreased += OnYielderUpdate;
      yielder.YieldAdded += OnYielderUpdate;
      var cuttable = yielder.GetComponent<Cuttable>();
      if (cuttable) {
        cuttable.WasCut += OnYielderUpdate;
      }
      var growable = yielder.GetComponent<Growable>();
      if (growable) {
        growable.HasGrown += OnYielderUpdate;
      }
      var living = yielder.GetComponent<LivingNaturalResource>();
      if (living) {
        living.Died += OnYielderUpdate;
        living.ReversedDeath += OnYielderUpdate;
      }
      _yieldersChanged = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RemoveYielder(Yielder yielder, bool isCleanup = false) {
      if (!_yielders.Remove(yielder)) {
        if (!isCleanup) {
          HostedDebugLog.Error(AutomationBehavior, "Yielder not found in the set: {0}", yielder);
        }
        return;
      }
      _yieldersChanged = true;
      if (!yielder) {
        // Due to a race condition, the building range update can happen before the entity deletion event. As a result,
        // there can be dead entries, which will be cleaned up at the end of the frame run.
        return;
      }
      yielder.YieldDecreased -= OnYielderUpdate;
      yielder.YieldAdded -= OnYielderUpdate;
      var cuttable = yielder.GetComponent<Cuttable>();
      if (cuttable) {
        cuttable.WasCut -= OnYielderUpdate;
      }
      var growable = yielder.GetComponent<Growable>();
      if (growable) {
        growable.HasGrown -= OnYielderUpdate;
      }
      var goodStack = yielder.GetComponent<GoodStack>();
      if (goodStack) {
        goodStack.GoodStackDisabled -= OnGoodStackDisabled;
      }
      var living = yielder.GetComponent<LivingNaturalResource>();
      if (living) {
        living.Died -= OnYielderUpdate;
        living.ReversedDeath -= OnYielderUpdate;
      }
    }

    void UpdateArea() {
      var newYielders = new HashSet<Yielder>();
      foreach (var coords in _buildingTerrainRange.GetRange()) {
        var blockObjects = _blockService.GetObjectsAt(coords);
        for (var i = blockObjects.Count - 1; i >= 0; i--) {
          var blockObject = blockObjects[i];
          var yielders = GetYieldersFromBlockObject(blockObject);
          if (yielders == null) {
            continue; // No yielders in this block object.
          }
          newYielders.AddRange(yielders);
        }
      }
      for (var i = _yielders.Count - 1; i >= 0; --i) {
        var yielder = _yielders.ElementAt(i);
        if (!newYielders.Contains(yielder)) {
          RemoveYielder(yielder);
        }
      }
      for (var i = newYielders.Count - 1; i >= 0; --i) {
        var yielder = newYielders.ElementAt(i);
        if (!_yielders.Contains(yielder)) {
          AddYielder(yielder);
        }
      }
    }

    HashSet<Yielder> GetYieldersFromBlockObject(BlockObject blockObject) {
      if (!_buildingTerrainRange.GetRange().Contains(blockObject.Coordinates)
          || _needsCuttingArea && !_treeCuttingArea.IsInCuttingArea(blockObject.Coordinates)) {
        return null;
      }
      var yielders = new List<Yielder>();
      blockObject.GetComponents(yielders);
      var res = new HashSet<Yielder>();
      for (var i = yielders.Count - 1; i >= 0; i--) {
        var yielder = yielders[i];
        if (!_yieldRemovingBuilding.IsAllowed(yielder.YielderSpec)) {
          continue;
        }
        res.Add(yielder);
      }
      return res;
    }

    #endregion

    #region Callbacks

    /// <summary>Monitors for changes in the building-reachable area.</summary>
    /// <remarks>It is handled in ticks, so it doesn't happen on pause.</remarks>
    void BuildingRangeChanged(object sender, RangeChangedEventArgs _) {
      _rangeChanged = true;
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for changes in the yielders amounts.</summary>
    void OnYielderUpdate(object sender, EventArgs _) {
      _yieldersChanged = true;
      ScheduleStateUpdate();
    }

    /// <summary>Monitors good stack changes when the cuttable was moved to it.</summary>
    /// <remarks>The stack inventory gets disabled when the last item was removed (delivered).</remarks>
    void OnGoodStackDisabled(object sender, EventArgs _) {
      _yieldersChanged = true;
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for the new plants.</summary>
    [OnEvent]
    public void OnEntityInitializedEvent(EntityInitializedEvent e) {
      var blockObject = e.Entity.GetComponent<BlockObject>();
      if (blockObject == null) {
        return;
      }
      var yielders = GetYieldersFromBlockObject(blockObject);
      if (yielders == null) {
        return;
      }
      foreach (var yielder in yielders) {
        AddYielder(yielder);
        ScheduleStateUpdate();
      }
    }

    /// <summary>Removes yielders from the deleted objects.</summary>
    [OnEvent]
    public void OnEntityDeletedEvent(EntityDeletedEvent e) {
      var blockObject = e.Entity.GetComponent<BlockObject>();
      if (blockObject == null) {
        return;
      }
      if (!_buildingTerrainRange.GetRange().Contains(blockObject.Coordinates)) {
        return;
      }
      var yielders = new List<Yielder>();
      blockObject.GetComponents(yielders);
      for (var i = yielders.Count - 1; i >= 0; i--) {
        RemoveYielder(yielders[i], isCleanup: true);
      }
    }

    /// <summary>Monitors for changes in the tree cutting area.</summary>
    [OnEvent]
    public void OnTreeCuttingAreaChangedEvent(TreeCuttingAreaChangedEvent e) {
      _rangeChanged = true;
      ScheduleStateUpdate();
    }

    #endregion
  }

  #endregion
}
