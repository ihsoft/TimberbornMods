// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Common;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Growing;
using Timberborn.SingletonSystem;
using Timberborn.Yielding;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class CollectableScriptableComponent : ScriptableComponentBase {

  const string CollectableReadySignalLocKey = "IgorZ.Automation.Scriptable.Collectable.Signal.CollectableReady";

  const string CollectableReadySignalName = "Collectable.Ready";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Collectable";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var yieldRemovingBuilding = GetYieldRemovingBuilding(behavior, throwIfNotFound: false);
    if (!yieldRemovingBuilding) {
      return [];
    }
    return [CollectableReadySignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    if (name != CollectableReadySignalName) {
      throw new ScriptError.ParsingError("Unknown signal: " + name);
    }
    GetYieldRemovingBuilding(behavior);
    return () => CollectableReadySignal(behavior);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    if (name != CollectableReadySignalName) {
      throw new ScriptError.ParsingError("Unknown signal: " + name);
    }
    GetYieldRemovingBuilding(behavior);
    return CollectableReadySignalDef;
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName != CollectableReadySignalName) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    GetYieldRemovingBuilding(host.Behavior);
    host.Behavior.GetOrCreate<GatherableTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<GatherableTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef CollectableReadySignalDef => _collectableReadySignalDef ??= new SignalDef {
      ScriptName = CollectableReadySignalName,
      DisplayName = Loc.T(CollectableReadySignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(0),
      },
  };
  SignalDef _collectableReadySignalDef;

  static ScriptValue CollectableReadySignal(AutomationBehavior behavior) {
    var tracker = behavior.GetOrCreate<GatherableTracker>();
    return ScriptValue.FromInt(tracker.ReadyYielders);
  }

  #endregion

  #region Implementation

  static YieldRemovingBuilding GetYieldRemovingBuilding(AutomationBehavior behavior, bool throwIfNotFound = true) {
    var yieldRemovingBuilding = behavior.GetComponentFast<YieldRemovingBuilding>();
    var buildingTerrainRange = behavior.GetComponentFast<BuildingTerrainRange>();
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

  sealed class GatherableTracker : AbstractStatusTracker, IFinishedStateListener {

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

    #region API

    /// <summary>Returns the number of tiles that offer collectable items.</summary>
    public int ReadyYielders => _activeYielders;

    #endregion

    #region Implementation

    EventBus _eventBus;
    BlockService _blockService;
    BuildingTerrainRange _buildingTerrainRange;
    YieldRemovingBuilding _yieldRemovingBuilding;
    TreeCuttingArea _treeCuttingArea;

    readonly HashSet<Yielder> _yielders = [];
    int _activeYielders;
    bool _yieldersChanged;
    bool _rangeChanged;
    bool _needsCuttingArea;

    [Inject]
    public void InjectDependencies(EventBus eventBus, BlockService blockService, TreeCuttingArea treeCuttingArea) {
      _eventBus = eventBus;
      _blockService = blockService;
      _treeCuttingArea = treeCuttingArea;

      // This component is added dynamically, so it won't get the finished state callback on a finished building.
      if (GetComponentFast<BlockObject>().IsFinished) {
        OnEnterFinishedState();
      }
    }

    void Awake() {
      _buildingTerrainRange = GetComponentFast<BuildingTerrainRange>();
      _yieldRemovingBuilding = GetComponentFast<YieldRemovingBuilding>();
      _needsCuttingArea = GetComponentFast<LumberjackFlagWorkplaceBehavior>();
    }

    void ScheduleStateUpdate() {
      _stateUpdateCoroutine ??= StartCoroutine(StateUpdateCoroutine());
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

      HostedDebugLog.Fine(this, "Recalculating {0} yielders", _yielders.Count);
      var oldState = _activeYielders;
      _activeYielders = 0;
      for (var i = _yielders.Count - 1; i >= 0; i--) {
        if (_yielders.ElementAt(i).IsYielding) {
          ++_activeYielders;
        }
      }
      if (oldState == _activeYielders) {
        return; // No change in the state.
      }
      ScheduleSignal(CollectableReadySignalName, ignoreErrors: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddYielder(Yielder yielder) {
      if (!_yielders.Add(yielder)) {
        HostedDebugLog.Error(this, "Yielder already exists in the set: {0}", yielder);
        return;
      }
      yielder.YieldDecreased += OnYielderUpdate;
      yielder.YieldAdded += OnYielderUpdate;
      var cuttable = yielder.GetComponentFast<Cuttable>();
      if (cuttable) {
        cuttable.WasCut += OnYielderUpdate;
      }
      var growable = yielder.GetComponentFast<Growable>();
      if (growable) {
        growable.HasGrown += OnYielderUpdate;
      }
      _yieldersChanged = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void RemoveYielder(Yielder yielder, bool isCleanup = false) {
      if (!_yielders.Remove(yielder)) {
        if (!isCleanup) {
          HostedDebugLog.Error(this, "Yielder not found in the set: {0}", yielder);
        }
        return;
      }
      yielder.YieldDecreased -= OnYielderUpdate;
      yielder.YieldAdded -= OnYielderUpdate;
      var cuttable = yielder.GetComponentFast<Cuttable>();
      if (cuttable) {
        cuttable.WasCut -= OnYielderUpdate;
      }
      var growable = yielder.GetComponentFast<Growable>();
      if (growable) {
        growable.HasGrown -= OnYielderUpdate;
      }
      _yieldersChanged = true;
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
      blockObject.GetComponentsFast(yielders);
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
    void BuildingRangeChanged(object sender, RangeChangedEventArgs args) {
      _rangeChanged = true;
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for changes in the yielders amounts.</summary>
    void OnYielderUpdate(object sender, EventArgs e) {
      _yieldersChanged = true;
      ScheduleStateUpdate();
    }

    /// <summary>Monitors for the new plants.</summary>
    [OnEvent]
    public void OnEntityInitializedEvent(EntityInitializedEvent e) {
      var blockObject = e.Entity.GetComponentFast<BlockObject>();
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
      var blockObject = e.Entity.GetComponentFast<BlockObject>();
      if (blockObject == null) {
        return;
      }
      if (!_buildingTerrainRange.GetRange().Contains(blockObject.Coordinates)) {
        return;
      }
      var yielders = new List<Yielder>();
      blockObject.GetComponentsFast(yielders);
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
