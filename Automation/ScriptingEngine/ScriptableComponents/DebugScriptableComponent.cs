// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.ResourceCountingSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class DebugScriptableComponent : ScriptableComponentBase {

  const string DistrictStockCapacityTrackerSignalLocKey = "IgorZ.Automation.Scriptable.Debug.Signal.DistrictStockCapacityTracker";
  const string DistrictStockTrackerSignalLocKey = "IgorZ.Automation.Scriptable.Debug.Signal.DistrictStockTracker";
  const string TickerSignalLocKey = "IgorZ.Automation.Scriptable.Debug.Signal.Ticker";
  const string LogStrActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogStr";
  const string LogNumActionLocKey = "IgorZ.Automation.Scriptable.Debug.Action.LogNum";

  const string DistrictStockTrackerSignalNamePrefix = "Debug.DistrictStockTracker.";
  const string TickerSignalName = "Debug.Ticker";
  const string LogStrActionName = "Debug.LogStr";
  const string LogNumActionName = "Debug.LogNum";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Debug";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    if (TryParseStockTrackerSignalName(name, out var goodSpec, out var isCapacity)) {
      return isCapacity
          ? () => StockTrackerCapacitySignal(behavior, goodSpec.Id)
          : () => StockTrackerValueSignal(behavior, goodSpec.Id);
    }
    return name switch {
        TickerSignalName => TickerSignal,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    if (TryParseStockTrackerSignalName(name, out var goodSpec, out var isCapacity)) {
      return isCapacity
          ? LookupSignalDef(name, () => MakeDistrictStockCapacityTrackerSignalDef(name, goodSpec))
          : LookupSignalDef(name, () => MakeDistrictStockTrackerSignalDef(name, goodSpec));
    }
    return name switch {
        TickerSignalName => TickerSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  readonly ReferenceManager _referenceManager = new ReferenceManager();

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.AddSignal(signalOperator, host);
    if (signalOperator.SignalName.StartsWith(DistrictStockTrackerSignalNamePrefix)) {
      _stockTickersRegistered++;
    }
    if (_referenceManager.Signals.Count == 1) {
      _automationService.RegisterTickable(OnTick);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.RemoveSignal(signalOperator, host);
    if (signalOperator.SignalName.StartsWith(DistrictStockTrackerSignalNamePrefix)) {
      _stockTickersRegistered--;
    }
    if (_referenceManager.Signals.Count == 0) {
      _automationService.UnregisterTickable(OnTick);
    }
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior _) {
    return [LogStrActionName, LogNumActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return name switch {
        LogStrActionName => args => LogStrAction(behavior, args),
        LogNumActionName => args => LogNumAction(behavior, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        LogStrActionName => LogStrActionDef,
        LogNumActionName => LogNumActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Signals

  const int ReasonableTickQuantifierMax = 10;

  SignalDef TickerSignalDef => _tickerSignalDef ??= new SignalDef {
      ScriptName = TickerSignalName,
      DisplayName = Loc.T(TickerSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(0, ReasonableTickQuantifierMax),
          ValueUiHint = GetArgumentMaxValueHint(ReasonableTickQuantifierMax),
      },
  };
  SignalDef _tickerSignalDef;

  SignalDef MakeDistrictStockTrackerSignalDef(string signalName, GoodSpec spec) {
    return new SignalDef {
        ScriptName = signalName,
        DisplayName = Loc.T(DistrictStockTrackerSignalLocKey, spec.PluralDisplayName.Value),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
        },
    };
  }

  SignalDef MakeDistrictStockCapacityTrackerSignalDef(string signalName, GoodSpec spec) {
    return new SignalDef {
        ScriptName = signalName,
        DisplayName = Loc.T(DistrictStockCapacityTrackerSignalLocKey, spec.PluralDisplayName.Value),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
        },
    };
  }

  ScriptValue TickerSignal() {
    return ScriptValue.Of(AutomationService.CurrentTick);
  }

  ScriptValue StockTrackerValueSignal(AutomationBehavior behavior, string goodId) {
    var districtCenter = behavior.GetComponentFast<DistrictBuilding>().InstantDistrict;
    if (districtCenter == null || !_districtStockCounter.TryGetValue(districtCenter, out var stockCounter)) {
      return ScriptValue.Of(0);
    }
    return ScriptValue.FromInt(stockCounter.GetInputOutputStock(goodId));
  }

  ScriptValue StockTrackerCapacitySignal(AutomationBehavior behavior, string goodId) {
    var districtCenter = behavior.GetComponentFast<DistrictBuilding>().InstantDistrict;
    if (districtCenter == null || !_districtCapacityCounter.TryGetValue(districtCenter, out var capacityCounter)) {
      return ScriptValue.Of(0);
    }
    return ScriptValue.FromInt(capacityCounter.GetInputOutputCapacity(goodId));
  }

  #endregion

  #region Actions

  ActionDef LogStrActionDef => _logActionDef ??= new ActionDef {
      ScriptName = LogStrActionName,
      DisplayName = Loc.T(LogStrActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _logActionDef;

  ActionDef LogNumActionDef => _logNumActionDef ??= new ActionDef {
      ScriptName = LogNumActionName,
      DisplayName = Loc.T(LogNumActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
          },
      ],
  };
  ActionDef _logNumActionDef;

  static void LogStrAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogStrActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug action]: {0}", args[0].AsString);
  }

  static void LogNumAction(BaseComponent instance, ScriptValue[] args) {
    AssertActionArgsCount(LogNumActionName, args, 1);
    HostedDebugLog.Info(instance, "[Debug action]: {0}", args[0].AsNumber);
  }

  #endregion

  #region Implemenation

  readonly Dictionary<DistrictCenter, StockCounter> _districtStockCounter = new();
  readonly Dictionary<DistrictCenter, CapacityCounter> _districtCapacityCounter = new();

  AutomationService _automationService;
  IGoodService _goodService;
  InventoryService _inventoryService;
  DistrictCenterRegistry _districtCenterRegistry;

  int _stockTickersRegistered;

  [Inject]
  public void InjectDependencies(AutomationService automationService, IGoodService goodService,
                                 InventoryService inventoryService, DistrictCenterRegistry districtCenterRegistry) {
    _automationService = automationService;
    _goodService = goodService;
    _inventoryService = inventoryService;
    _districtCenterRegistry = districtCenterRegistry;
  }

  bool TryParseStockTrackerSignalName(string signalName, out GoodSpec goodSpec, out bool isCapacity) {
    if (!signalName.StartsWith(DistrictStockTrackerSignalNamePrefix)) {
      goodSpec = null;
      isCapacity = false;
      return false;
    }
    var parts = signalName[DistrictStockTrackerSignalNamePrefix.Length..].Split('.');
    if (parts.Length is < 1 or > 2) {
      throw new UnknownSignalException(signalName);
    }
    isCapacity = false;
    if (parts.Length > 1) {
      if (parts[1] != "Capacity") {
        throw new UnknownSignalException(signalName);
      }
      isCapacity = true;
    }
    goodSpec = _goodService.GetGoodOrNull(parts[0]);
    if (goodSpec == null) {
      throw new UnknownSignalException(signalName);
    }
    return true;
  }

  void UpdateDistrictResources() {
    _districtStockCounter.Clear();
    _districtCapacityCounter.Clear();
    foreach (var districtCenter in _districtCenterRegistry.FinishedDistrictCenters) {
      var inventories = _inventoryService.PublicOutputInventories
          .Where(inventory => inventory.GetComponentFast<DistrictBuilding>().InstantDistrict == districtCenter)
          .ToArray();
      var stockCounter = new StockCounter();
      stockCounter.UpdateStock(inventories);
      _districtStockCounter.Add(districtCenter, stockCounter);
      var capacityCounter = new CapacityCounter();
      capacityCounter.UpdateCapacity(inventories);
      _districtCapacityCounter.Add(districtCenter, capacityCounter);
    }
  }

  void OnTick(int currentTick) {
    if (_stockTickersRegistered > 0) {
      UpdateDistrictResources();
      var listeners = _referenceManager.Signals
          .Where(pair => pair.Value.Any(x => x.SignalName.StartsWith(DistrictStockTrackerSignalNamePrefix)))
          .Select(x => x.Key)
          .ToList();  // Need copy!
      foreach (var listener in listeners) {
        ScriptingService.ScheduleSignalCallback(
            new ScriptingService.SignalCallback(DistrictStockTrackerSignalNamePrefix, listener),
            ignoreErrors: true);
      }
    }
    if (_referenceManager.Signals.Count > 0) {
      _referenceManager.ScheduleSignal(TickerSignalName, ScriptingService, ignoreErrors: true);
    }
  }

  #endregion
}
