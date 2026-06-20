// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.MechanicalSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class PowerScriptableComponent : ScriptableComponentBase {

  const string SupplySignalLocKey = "IgorZ.Automation.Scriptable.Power.Signal.Supply";
  const string DemandSignalLocKey = "IgorZ.Automation.Scriptable.Power.Signal.Demand";
  const string BatteryChargeSignalLocKey = "IgorZ.Automation.Scriptable.Power.Signal.BatteryCharge";
  const string BatteryCapacitySignalLocKey = "IgorZ.Automation.Scriptable.Power.Signal.BatteryCapacity";
  const string BatteryChargeLevelSignalLocKey = "IgorZ.Automation.Scriptable.Power.Signal.BatteryChargeLevel";

  const string SupplySignalName = "Power.Supply";
  const string DemandSignalName = "Power.Demand";
  const string BatteryChargeSignalName = "Power.BatteryCharge";
  const string BatteryCapacitySignalName = "Power.BatteryCapacity";
  const string BatteryChargeLevelSignalName = "Power.BatteryChargeLevel";

  static readonly string[] SignalNames = [
      SupplySignalName,
      DemandSignalName,
      BatteryChargeSignalName,
      BatteryCapacitySignalName,
      BatteryChargeLevelSignalName,
  ];

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Power";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<MechanicalNode>() ? SignalNames : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var mechanicalNode = GetComponentOrThrow<MechanicalNode>(behavior);
    return name switch {
        SupplySignalName => () => SupplySignal(mechanicalNode),
        DemandSignalName => () => DemandSignal(mechanicalNode),
        BatteryChargeSignalName => () => BatteryChargeSignal(mechanicalNode),
        BatteryCapacitySignalName => () => BatteryCapacitySignal(mechanicalNode),
        BatteryChargeLevelSignalName => () => BatteryChargeLevelSignal(mechanicalNode),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    GetComponentOrThrow<MechanicalNode>(behavior);  // Verify only.
    return name switch {
        SupplySignalName => SupplySignalDef,
        DemandSignalName => DemandSignalDef,
        BatteryChargeSignalName => BatteryChargeSignalDef,
        BatteryCapacitySignalName => BatteryCapacitySignalDef,
        BatteryChargeLevelSignalName => BatteryChargeLevelSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (!IsKnownSignal(signalOperator.SignalName)) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    GetComponentOrThrow<MechanicalNode>(host.Behavior);
    var tracker = host.Behavior.GetOrCreate<PowerTracker>();
    var wasTracking = tracker.HasSignals;
    tracker.AddSignal(signalOperator, host);
    if (!wasTracking) {
      _trackers.Add(tracker);
      if (_trackers.Count == 1) {
        _automationService.RegisterTickable(OnTick);
      }
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (!IsKnownSignal(signalOperator.SignalName)) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    var tracker = host.Behavior.GetOrThrow<PowerTracker>();
    tracker.RemoveSignal(signalOperator, host);
    if (!tracker.HasSignals) {
      _trackers.Remove(tracker);
      if (_trackers.Count == 0) {
        _automationService.UnregisterTickable(OnTick);
      }
    }
  }

  #endregion

  #region Signals

  SignalDef SupplySignalDef => _supplySignalDef ??= new SignalDef {
      ScriptName = SupplySignalName,
      DisplayName = Loc.T(SupplySignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = IntegerSignalValueDef,
  };
  SignalDef _supplySignalDef;

  SignalDef DemandSignalDef => _demandSignalDef ??= new SignalDef {
      ScriptName = DemandSignalName,
      DisplayName = Loc.T(DemandSignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = IntegerSignalValueDef,
  };
  SignalDef _demandSignalDef;

  SignalDef BatteryChargeSignalDef => _batteryChargeSignalDef ??= new SignalDef {
      ScriptName = BatteryChargeSignalName,
      DisplayName = Loc.T(BatteryChargeSignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = IntegerSignalValueDef,
  };
  SignalDef _batteryChargeSignalDef;

  SignalDef BatteryCapacitySignalDef => _batteryCapacitySignalDef ??= new SignalDef {
      ScriptName = BatteryCapacitySignalName,
      DisplayName = Loc.T(BatteryCapacitySignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = IntegerSignalValueDef,
  };
  SignalDef _batteryCapacitySignalDef;

  SignalDef BatteryChargeLevelSignalDef => _batteryChargeLevelSignalDef ??= new SignalDef {
      ScriptName = BatteryChargeLevelSignalName,
      DisplayName = Loc.T(BatteryChargeLevelSignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Percent,
          DisplayNumericFormatRange = (0, 100),
          RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0f, max: 1f),
      },
  };
  SignalDef _batteryChargeLevelSignalDef;

  static ValueDef IntegerSignalValueDef => new() {
      ValueType = ScriptValue.TypeEnum.Number,
      DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
      DisplayNumericFormatRange = (0, float.NaN),
  };

  static ScriptValue SupplySignal(MechanicalNode mechanicalNode) {
    return ScriptValue.FromInt(mechanicalNode.Graph?.PowerSupply ?? 0);
  }

  static ScriptValue DemandSignal(MechanicalNode mechanicalNode) {
    return ScriptValue.FromInt(mechanicalNode.Graph?.PowerDemand ?? 0);
  }

  static ScriptValue BatteryChargeSignal(MechanicalNode mechanicalNode) {
    return ScriptValue.FromInt(mechanicalNode.Graph?.BatteryCharge ?? 0);
  }

  static ScriptValue BatteryCapacitySignal(MechanicalNode mechanicalNode) {
    return ScriptValue.FromInt(mechanicalNode.Graph?.BatteryCapacity ?? 0);
  }

  static ScriptValue BatteryChargeLevelSignal(MechanicalNode mechanicalNode) {
    return ScriptValue.FromFloat(mechanicalNode.Graph?.BatteryChargeLevel ?? 0f);
  }

  #endregion

  #region Implementation

  readonly AutomationService _automationService;
  readonly HashSet<PowerTracker> _trackers = [];

  PowerScriptableComponent(AutomationService automationService) {
    _automationService = automationService;
  }

  static bool IsKnownSignal(string signalName) {
    return signalName is SupplySignalName or DemandSignalName or BatteryChargeSignalName
        or BatteryCapacitySignalName or BatteryChargeLevelSignalName;
  }

  void OnTick(int currentTick) {
    foreach (var tracker in new List<PowerTracker>(_trackers)) {
      tracker.UpdateSignals();
    }
  }

  #endregion

  #region Power tracker component

  internal sealed class PowerTracker : AbstractStatusTracker {

    #region API

    public bool HasSignals => _trackedSignalNames.Count > 0;

    public void UpdateSignals() {
      foreach (var signalName in new List<string>(_trackedSignalNames)) {
        TriggerSignalUpdate(signalName);
      }
    }

    #endregion

    #region AbstractStatusTracker overrides

    /// <inheritdoc/>
    public override bool AddSignal(SignalOperator signalOperator, ISignalListener host) {
      var result = base.AddSignal(signalOperator, host);
      _trackedSignalNames.Add(signalOperator.SignalName);
      return result;
    }

    /// <inheritdoc/>
    public override bool RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
      var hasMoreListenersForSignal = base.RemoveSignal(signalOperator, host);
      if (!hasMoreListenersForSignal) {
        _trackedSignalNames.Remove(signalOperator.SignalName);
      }
      return hasMoreListenersForSignal;
    }

    #endregion

    #region Implementation

    readonly HashSet<string> _trackedSignalNames = [];

    #endregion
  }

  #endregion
}
