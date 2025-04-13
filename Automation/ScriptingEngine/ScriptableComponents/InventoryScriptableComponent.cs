// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Collections.Generic;
using Bindito.Core;
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
        throw new ScriptError($"Input good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
      return () => ScriptValue.FromInt(inventory.AmountInStock(goodId));
    }
    if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      var goodId = name[OutputGoodSignalNamePrefix.Length..];
      if (!inventory.OutputGoods.Contains(goodId)) {
        throw new ScriptError($"Output good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
      return () => ScriptValue.FromInt(inventory.AmountInStock(goodId));
    }
    throw new ScriptError("Unknown signal: " + name);
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
      throw new ScriptError("Unknown trigger: " + name);
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
      throw new ScriptError("Building is not emptiable: " + DebugEx.ObjectToString(building));
    }
    return name switch {
        StartEmptyingStockActionName => _ => emptiable.MarkForEmptyingWithoutStatus(),
        StopEmptyingStockActionName => _ => emptiable.UnmarkForEmptying(),
        _ => throw new ScriptError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    if (!name.StartsWith(InputGoodSignalNamePrefix) && !name.StartsWith(OutputGoodSignalNamePrefix)) {
      throw new ScriptError("Unknown signal: " + name);
    }
    var inventory = GetInventory(building);
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      var goodId = name[InputGoodSignalNamePrefix.Length..];
      if (!inventory.InputGoods.Contains(goodId)) {
        throw new ScriptError($"Input good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
    }
    if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      var goodId = name[OutputGoodSignalNamePrefix.Length..];
      if (!inventory.OutputGoods.Contains(goodId)) {
        throw new ScriptError($"Output good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
    }
    var tracker = building.GetComponentFast<InventoryChangeTracker>()
        ?? _instantiator.AddComponent<InventoryChangeTracker>(building.GameObjectFast);
    tracker.SignalChangeCallbacks.Add(onValueChanged);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    var tracker = building.GetComponentFast<InventoryChangeTracker>();
    if (tracker) {
      tracker.SignalChangeCallbacks.Remove(onValueChanged);
    }
  }

  /// <inheritdoc/>
  public override void InstallAction(string name, BaseComponent building) {
    if (name is StartEmptyingStockActionName or StopEmptyingStockActionName) {
      var behavior = building.GetComponentFast<EmptyingStatusBehavior>();
      if (behavior == null) {
        behavior = _instantiator.AddComponent<EmptyingStatusBehavior>(building.GameObjectFast);
      }
      behavior.AddReference();
    } else {
      throw new ScriptError("Unknown action: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UninstallAction(string name, BaseComponent building) {
    if (name is StartEmptyingStockActionName or StopEmptyingStockActionName) {
      var behavior = building.GetComponentFast<EmptyingStatusBehavior>();
      if (behavior == null) {
        throw new InvalidOperationException("Status behavior not found on: " + DebugEx.ObjectToString(building));
      }
      behavior.RemoveReference();
    } else {
      throw new ScriptError("Unknown action: " + name);
    }
  }

  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        StartEmptyingStockActionName => StartEmptyingStockActionDef,
        StopEmptyingStockActionName => StopEmptyingStockActionDef,
        _ => throw new ScriptError("Unknown action: " + name),
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
        throw new ScriptError("Inventories component not found on: " + DebugEx.ObjectToString(building));
      }
      return null;
    }
    var inventory = inventories.AllInventories
        .FirstOrDefault(x => x.ComponentName != ConstructionSiteInventoryInitializer.InventoryComponentName);
    if (inventory == null && throwIfNotFound) {
      throw new ScriptError("Inventory component not found on: " + DebugEx.ObjectToString(building));
    }
    return inventory;
  }

  #endregion

  #region Inventory change tracker component

  sealed class InventoryChangeTracker : BaseComponent {
    public readonly List<Action> SignalChangeCallbacks = [];

    void Awake() {
      GetInventory(this).InventoryStockChanged += (_, _) => NotifyChange();
    }

    void NotifyChange() {
      foreach (var callback in SignalChangeCallbacks) {
        callback();
      }
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
    int _installedActions;

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

    public void AddReference() {
      _installedActions++;
      if (_installedActions == 1 && _statusToggle != null) {  // On game load, it is called before the Start() event.
        RefreshStatus();
      }
    }

    public void RemoveReference() {
      if (_installedActions == 0) {;
        throw new InvalidOperationException("Uninstalling more actions than installed");
      }
      _installedActions--;
      if (_installedActions == 0 && _emptiable.IsMarkedForEmptying) {
        _emptiable.UnmarkForEmptying();
      }
    }

    void RefreshStatus() {
      if (_installedActions == 0) {
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
