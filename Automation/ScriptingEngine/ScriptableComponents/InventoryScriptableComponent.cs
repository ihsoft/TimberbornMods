// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.ConstructionSites;
using Timberborn.Emptying;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.StatusSystem;
using Timberborn.Yielding;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class InventoryScriptableComponent : ScriptableComponentBase {

  const string InputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.InputGood";
  const string OutputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.OutputGood";
  const string StartEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StartEmptyingStock";
  const string StopEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StopEmptyingStock";
  const string EmptyingStatusDescriptionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.EmptyingStatus";

  const string InputGoodSignalNamePrefix = "Inventory.InputGood.";
  const string OutputGoodSignalNamePrefix = "Inventory.OutputGood.";
  const string StartEmptyingStockActionName = "Inventory.StartEmptying";
  const string StopEmptyingStockActionName = "Inventory.StopEmptying";

  const string EmptyingStatusIcon = "IgorZ.Automation/status-icon-emptying";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Inventory";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var inventory = GetInventory(behavior, throwIfNotFound: false);
    if (!inventory) {
      return [];
    }
    if (inventory._goodDisallower is InRangeYielderGoodAllower) {
      // Yielders don't know which goods they allow until they find them the first time. Return all the allowed goods.
      return inventory.AllowedGoods.Select(x => MakeSignalName(x.StorableGood.GoodId, inventory))
          .ToArray();
    }
    List<GoodAmount> capacities = [];
    inventory.GetCapacity(capacities);
    return capacities.Select(x => MakeSignalName(x.GoodId, inventory)).ToArray();
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var parsed = ParseSignalName(name, behavior);
    var inventory = GetInventory(behavior);
    return () => GoodAmountSignal(parsed.goodId, inventory);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var parsed = ParseSignalName(name, behavior);
    var key = parsed.capacity + "-" + name;
    return LookupSignalDef(key, () => MakeSignalDef(name, behavior));
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    if (!behavior.GetComponentFast<Emptiable>()) {
      return [];
    }
    var inventory = GetInventory(behavior, throwIfNotFound: false);
    if (!inventory.IsOutput) {
      return [];
    }
    // Don't allow emptying buildings with inputs (ingredients) since the input will also be emptied (same as pause).
    foreach (var inputGood in inventory.InputGoods) {
      if (!inventory.OutputGoods.Contains(inputGood)) {
        return [];
      }
    }
    return [StartEmptyingStockActionName, StopEmptyingStockActionName];
  }
  
  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var emptiable = behavior.GetComponentFast<Emptiable>();
    if (!emptiable) {
      throw new ScriptError.BadStateError(behavior, "Building is not emptiable");
    }
    return name switch {
        StartEmptyingStockActionName => _ => StartEmptyingStockAction(emptiable),
        StopEmptyingStockActionName => _ => StopEmptyingStockAction(emptiable),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        StartEmptyingStockActionName => StartEmptyingStockActionDef,
        StopEmptyingStockActionName => StopEmptyingStockActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    ParseSignalName(signalOperator.SignalName, host.Behavior, throwErrors: true);
    host.Behavior.GetOrCreate<InventoryChangeTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<InventoryChangeTracker>().RemoveSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is StartEmptyingStockActionName or StopEmptyingStockActionName) {
      behavior.GetOrCreate<EmptyingStatusBehavior>().AddAction(actionOperator);
    }
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is StartEmptyingStockActionName or StopEmptyingStockActionName) {
      behavior.GetOrThrow<EmptyingStatusBehavior>().RemoveAction(actionOperator);
    }
  }

  #endregion

  #region Signals

  SignalDef MakeSignalDef(string name, AutomationBehavior behavior) {
    var parsed = ParseSignalName(name, behavior);
    var displayName = LocGoodSignal(parsed.isInput ? InputGoodSignalLocKey : OutputGoodSignalLocKey, parsed.goodId);
    return new SignalDef {
        ScriptName = name,
        DisplayName = displayName,
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            ValueValidator = ValueDef.RangeCheckValidatorInt(0, parsed.capacity),
            ValueUiHint = GetArgumentMaxValueHint(parsed.capacity),
        },
    };
  }

  static ScriptValue GoodAmountSignal(string goodId, Inventory inventory) {
    return ScriptValue.FromInt(inventory.AmountInStock(goodId));
  }

  #endregion

  #region Actions

  ActionDef StartEmptyingStockActionDef => _startEmptyingStockActionDef ??= new ActionDef {
      ScriptName = StartEmptyingStockActionName,
      DisplayName = Loc.T(StartEmptyingStockActionLocKey),
      Arguments = [],
  };
  ActionDef _startEmptyingStockActionDef;

  ActionDef StopEmptyingStockActionDef => _stopEmptyingStockActionDef ??= new ActionDef {
      ScriptName = StopEmptyingStockActionName,
      DisplayName = Loc.T(StopEmptyingStockActionLocKey),
      Arguments = [],
  };
  ActionDef _stopEmptyingStockActionDef;

  static void StartEmptyingStockAction(Emptiable emptiable) {
    if (!emptiable.IsMarkedForEmptying) {
      emptiable.MarkForEmptyingWithoutStatus();
    }
  }

  static void StopEmptyingStockAction(Emptiable emptiable) {
    if (emptiable.IsMarkedForEmptying) {
      emptiable.UnmarkForEmptying();
    }
  }

  #endregion

  #region Implementation

  readonly IGoodService _goodService;

  InventoryScriptableComponent(IGoodService goodService, BaseInstantiator instantiator) {
    _goodService = goodService;
  }

  static string MakeSignalName(string goodId, Inventory inventory) {
    var prefix = inventory.OutputGoods.Contains(goodId) ? OutputGoodSignalNamePrefix : InputGoodSignalNamePrefix;
    return prefix + goodId;
  }

  static (bool isInput, string goodId, int capacity) ParseSignalName(
      string name, AutomationBehavior behavior, bool throwErrors = false) {
    var inventory = GetInventory(behavior);
    string goodId = null;
    var isInput = false;
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      isInput = true;
      goodId = name[InputGoodSignalNamePrefix.Length..];
      if (!inventory.InputGoods.Contains(goodId)) {
        goodId = null;
      }
    } else if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      goodId = name[OutputGoodSignalNamePrefix.Length..];
      if (!inventory.OutputGoods.Contains(goodId)) {
        goodId = null;
      }
    }
    if (goodId == null) {
      if (throwErrors) {
        throw new InvalidOperationException("Unknown signal: " + name);
      }
      throw new ScriptError.BadStateError(inventory, "Signal not supported: " + name);
    }
    // Yielders don't know about the good until they find it the first time.
    var forceAllowedGoods = inventory._goodDisallower is InRangeYielderGoodAllower;
    return forceAllowedGoods
        ? (isInput, goodId, inventory.AllowedGoods.First(x => x.StorableGood.GoodId == goodId).Amount)
        : (isInput, goodId, inventory.LimitedAmount(goodId));
  }

  string LocGoodSignal(string name, string goodId) {
    return Loc.T(name, _goodService.GetGood(goodId).PluralDisplayName.Value);
  }

  internal static Inventory GetInventory(BaseComponent building, bool throwIfNotFound = true) {
    var inventories = building.GetComponentFast<Inventories>();
    if (!inventories) {
      if (throwIfNotFound) {
        throw new ScriptError.BadStateError(building, "Inventories component not found");
      }
      return null;
    }
    var inventory = inventories.AllInventories
        .FirstOrDefault(x => x.ComponentName != ConstructionSiteInventoryInitializer.InventoryComponentName);
    if (!inventory && throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Inventory component not found");
    }
    return inventory;
  }

  #endregion

  #region Inventory change tracker component

  sealed class InventoryChangeTracker : AbstractStatusTracker {

    Inventory _inventory;

    void Awake() {
      _inventory = GetInventory(this, throwIfNotFound: false);
      if (!_inventory) {
        throw new InvalidOperationException("Inventory component not found on: " + DebugEx.ObjectToString(this));
      }
      _inventory.InventoryStockChanged += NotifyChange;
      _inventory.InventoryCapacityReservationChanged += (_, args) => {
        HostedDebugLog.Warning(_inventory, "Capacity reservation changed: {0}", _inventory.ReservedCapacity(args.GoodAmount.GoodId));
      };
      //FIXME: react on inventory change to recompile the scripts, how? send a signal to recompile all on the behavior?
    }

    void NotifyChange(object sender, InventoryAmountChangedEventArgs args) {
      ScheduleSignal(MakeSignalName(args.GoodAmount.GoodId, _inventory), ignoreErrors: true);
    }
  }

  #endregion

  #region Emptying status presenter

  /// <summary>
  /// Creates a custom status icon that indicates that the storage is being emptying. If the status is changed
  /// externally, then hides the status and notifies the action.
  /// </summary>
  sealed class EmptyingStatusBehavior : AbstractStatusTracker {

    ILoc _loc;
    StatusToggle _statusToggle;
    Emptiable _emptiable;

    /// <inheritdoc/>
    protected override void OnFirstReference() {
      if (_statusToggle != null) {  // On game load, add ref is called before the Start() event gets executed.
        RefreshStatus();
      }
    }

    /// <inheritdoc/>
    protected override void OnLastReference() {
      if (_emptiable.IsMarkedForEmptying) {
        _emptiable.UnmarkForEmptying();
      }
    }

    [Inject]
    public void InjectDependencies(ILoc loc) {
      _loc = loc;
    }

    void Start() {
      _emptiable = GetComponentFast<Emptiable>();
      _emptiable.UnmarkedForEmptying += (_, _) => RefreshStatus();
      _emptiable.MarkedForEmptying += (_, _) => RefreshStatus();
      _statusToggle = StatusToggle.CreatePriorityStatusWithFloatingIcon(
          EmptyingStatusIcon, _loc.T(EmptyingStatusDescriptionLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_statusToggle);
      RefreshStatus();
    }

    void RefreshStatus() {
      if (!HasActions) {
        _statusToggle.Deactivate();
        return;
      }
      if (_emptiable.IsMarkedForEmptying) {
        _statusToggle.Activate();
      } else {
        _statusToggle.Deactivate();
      }
    }
  }

  #endregion
}
