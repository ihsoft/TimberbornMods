// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.ConstructionSites;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class InventoryScriptableComponent : ScriptableComponentBase {

  const string InputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.InputGood";
  const string OutputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.OutputGood";

  const string InputGoodSignalNamePrefix = "Inventory.InputGood.";
  const string OutputGoodSignalNamePrefix = "Inventory.OutputGood.";

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
      var goodId = name.Substring(InputGoodSignalNamePrefix.Length);
      if (!inventory.InputGoods.Contains(goodId)) {
        throw new ScriptError($"Input good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
      return () => ScriptValue.Of(inventory.AmountInStock(goodId) * 100);
    }
    if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      var goodId = name.Substring(OutputGoodSignalNamePrefix.Length);
      if (!inventory.OutputGoods.Contains(goodId)) {
        throw new ScriptError($"Output good '{goodId}' not found in: {DebugEx.ObjectToString(inventory)}");
      }
      return () => ScriptValue.Of(inventory.AmountInStock(goodId) * 100);
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
      displayName = LocGoodSignal(InputGoodSignalLocKey, name.Substring(InputGoodSignalNamePrefix.Length));
    } else if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      displayName = LocGoodSignal(OutputGoodSignalLocKey, name.Substring(OutputGoodSignalNamePrefix.Length));
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
  public override void RegisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
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

  #endregion

  #region Signals

  readonly Dictionary<string, SignalDef> _signalDefs = [];

  #endregion

  #region Implementation

  readonly IGoodService _goodService;
  readonly BaseInstantiator _instantiator;

  InventoryScriptableComponent(IGoodService goodService, BaseInstantiator instantiator) {
    _goodService = goodService;
    _instantiator = instantiator;
  }

  string LocGoodSignal(string name, string goodId) {
    return Loc.T(name, _goodService.GetGood(goodId).PluralDisplayName);
  }

  static Inventory GetInventory(BaseComponent building, bool throwIfNotFound = true) {
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
      var inventory = GetEnabledComponent<Inventory>();
      inventory.InventoryStockChanged += (_, _) => NotifyChange();
    }
    
    void NotifyChange() {
      foreach (var callback in SignalChangeCallbacks) {
        callback();
      }
    }
  }

  #endregion
}
