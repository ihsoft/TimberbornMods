// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.ConstructionSites;
using Timberborn.Emptying;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.StatusSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class InventoryScriptableComponent : ScriptableComponentBase {

  const string InputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.InputGood";
  const string OutputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.OutputGood";
  const string StartEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StartEmptyingStock";
  const string StopEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StopEmptyingStock";
  const string EmptyingStatusDescriptionKey = "IgorZ.Automation.Scriptable.Inventory.Action.EmptyingStatus";

  const string InputGoodSignalNamePrefix = "Inventory.InputGood.";
  const string OutputGoodSignalNamePrefix = "Inventory.OutputGood.";
  const string StartEmptyingStockActionName = "Inventory.StartEmptying";
  const string StopEmptyingStockActionName = "Inventory.StopEmptying";

  const string EmptyingStatusIcon = "IgorZ.Automation/status-icon-emptying";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Inventory";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(BaseComponent building) {
    var inventory = GetInventory(building, throwIfNotFound: false);
    if (inventory == null) {
      return [];
    }
    var res = new List<string>();
    foreach (var good in inventory.AllowedGoods) {
      var prefix = inventory.OutputGoods.Contains(good.StorableGood.GoodId)
          ? OutputGoodSignalNamePrefix
          : InputGoodSignalNamePrefix;
      res.Add(prefix + good.StorableGood.GoodId);
    }
    return res.ToArray();
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    var inventory = GetInventory(building);
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      var goodId = name[InputGoodSignalNamePrefix.Length..];
      if (!inventory.InputGoods.Contains(goodId)) {
        throw new ScriptError.BadStateError(inventory, $"Input good '{goodId}' not found");
      }
      return () => ScriptValue.FromInt(inventory.AmountInStock(goodId));
    }
    if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      var goodId = name[OutputGoodSignalNamePrefix.Length..];
      if (!inventory.OutputGoods.Contains(goodId)) {
        throw new ScriptError.BadStateError(inventory, $"Output good '{goodId}' not found");
      }
      return () => ScriptValue.FromInt(inventory.AmountInStock(goodId));
    }
    throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, BaseComponent _) {
    if (_signalDefs.TryGetValue(name, out var def)) {
      return def;
    }
    string displayName = null;
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      displayName = LocGoodSignal(InputGoodSignalLocKey, name[InputGoodSignalNamePrefix.Length..]);
    } else if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      displayName = LocGoodSignal(OutputGoodSignalLocKey, name[OutputGoodSignalNamePrefix.Length..]);
    }
    if (displayName == null) {
      throw new ScriptError.ParsingError("Unknown signal: " + name);
    }

    def = new SignalDef {
        ScriptName = name,
        DisplayName = displayName,
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
        },
    };
    _signalDefs[name] = def;
    return def;
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    if (!building.GetComponentFast<Emptiable>()) {
      return [];
    }
    var inventory = GetInventory(building, throwIfNotFound: false);
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
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var emptiable = building.GetComponentFast<Emptiable>();
    if (!emptiable) {
      throw new ScriptError.BadStateError(building, "Building is not emptiable");
    }
    return name switch {
        StartEmptyingStockActionName => _ => emptiable.MarkForEmptyingWithoutStatus(),
        StopEmptyingStockActionName => _ => emptiable.UnmarkForEmptying(),
        _ => throw new ScriptError.ParsingError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (!name.StartsWith(InputGoodSignalNamePrefix) && !name.StartsWith(OutputGoodSignalNamePrefix)) {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
    var building = host.Behavior;
    var inventory = GetInventory(building);
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      var goodId = name[InputGoodSignalNamePrefix.Length..];
      if (!inventory.InputGoods.Contains(goodId)) {
        throw new InvalidOperationException($"{DebugEx.ObjectToString(inventory)} Input good '{goodId}' not found");
      }
    }
    if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      var goodId = name[OutputGoodSignalNamePrefix.Length..];
      if (!inventory.OutputGoods.Contains(goodId)) {
        throw new InvalidOperationException($"{DebugEx.ObjectToString(inventory)} Output good '{goodId}' not found");
      }
    }
    var tracker = building.GetComponentFast<InventoryChangeTracker>()
        ?? _instantiator.AddComponent<InventoryChangeTracker>(building.GameObjectFast);
    tracker.ReferenceManager.AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var tracker = host.Behavior.GetComponentFast<InventoryChangeTracker>();
    if (!tracker) {
      throw new InvalidOperationException(
          "Inventory change tracker not found on: " + DebugEx.ObjectToString(host.Behavior));
    }
    tracker.ReferenceManager.RemoveSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, BaseComponent building) {
    if (actionOperator.ActionName is not (StartEmptyingStockActionName or StopEmptyingStockActionName)) {
      throw new InvalidOperationException("Unknown action: " + actionOperator.ActionName);
    }
    var behavior = building.GetComponentFast<EmptyingStatusBehavior>()
        ?? _instantiator.AddComponent<EmptyingStatusBehavior>(building.GameObjectFast); 
    behavior.AddReference(actionOperator);
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, BaseComponent building) {
    if (actionOperator.ActionName is not (StartEmptyingStockActionName or StopEmptyingStockActionName)) {
      return;
    }
    var behavior = building.GetComponentFast<EmptyingStatusBehavior>();
    if (behavior == null) {
      throw new InvalidOperationException("Status behavior not found on: " + DebugEx.ObjectToString(building));
    }
    behavior.RemoveReference(actionOperator);
  }

  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        StartEmptyingStockActionName => StartEmptyingStockActionDef,
        StopEmptyingStockActionName => StopEmptyingStockActionDef,
        _ => throw new ScriptError.ParsingError("Unknown action: " + name),
    };
  }

  #endregion

  #region Signals

  readonly Dictionary<string, SignalDef> _signalDefs = [];

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

  #endregion

  #region Implementation

  readonly IGoodService _goodService;
  readonly BaseInstantiator _instantiator;

  InventoryScriptableComponent(IGoodService goodService, BaseInstantiator instantiator) {
    _goodService = goodService;
    _instantiator = instantiator;
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
    if (inventory == null && throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Inventory component not found");
    }
    return inventory;
  }

  #endregion

  #region Inventory change tracker component

  sealed class InventoryChangeTracker : BaseComponent {
    public readonly ReferenceManager ReferenceManager = new();

    ScriptingService _scriptingService;
    Inventory _inventory;

    [Inject]
    public void InjectDependencies(ScriptingService scriptingService) {
      _scriptingService = scriptingService;
    }

    void Awake() {
      _inventory = GetInventory(this, throwIfNotFound: false);
      if (_inventory == null) {
        throw new InvalidOperationException("Inventory component not found on: " + DebugEx.ObjectToString(this));
      }
      _inventory.InventoryStockChanged += NotifyChange;
    }

    void NotifyChange(object sender, InventoryAmountChangedEventArgs args) {
      var goodId = args.GoodAmount.GoodId;
      var prefix = _inventory.OutputGoods.Contains(goodId)
          ? OutputGoodSignalNamePrefix
          : InputGoodSignalNamePrefix;
      ReferenceManager.ScheduleSignal(prefix + goodId, _scriptingService);
    }
  }

  #endregion

  #region Emptying status presenter

  /// <summary>
  /// Creates a custom status icon that indicates that the storage is being emptying. If the status is changed
  /// externally, then hides the status and notifies the action.
  /// </summary>
  sealed class EmptyingStatusBehavior : BaseComponent {
    ILoc _loc;

    StatusToggle _statusToggle;
    Emptiable _emptiable;
    readonly HashSet<ActionOperator> _installedActions = [];

    [Inject]
    public void InjectDependencies(ILoc loc) {
      _loc = loc;
    }

    void Start() {
      _emptiable = GetComponentFast<Emptiable>();
      _emptiable.UnmarkedForEmptying += (_, _) => RefreshStatus();
      _emptiable.MarkedForEmptying += (_, _) => RefreshStatus();
      _statusToggle =
          StatusToggle.CreatePriorityStatusWithFloatingIcon(EmptyingStatusIcon, _loc.T(EmptyingStatusDescriptionKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_statusToggle);
      RefreshStatus();
    }

    public void AddReference(ActionOperator actionOperator) {
      if (!_installedActions.Add(actionOperator)) {
        throw new InvalidOperationException("Installing the same action multiple times: " + actionOperator);
      }
      if (_statusToggle != null) {  // On game load, add ref is called before the Start() event gets executed.
        RefreshStatus();
      }
    }

    public void RemoveReference(ActionOperator actionOperator) {
      if (!_installedActions.Remove(actionOperator)) {
        throw new InvalidOperationException("Uninstalling non-registered action: " + actionOperator);
      }
      if (_installedActions.Count == 0 && _emptiable.IsMarkedForEmptying) {
        _emptiable.UnmarkForEmptying();
      }
    }

    void RefreshStatus() {
      if (_installedActions.Count == 0) {
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
