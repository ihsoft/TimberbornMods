// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.Emptying;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.StatusSystem;
using Timberborn.StockpilePrioritySystem;
using Timberborn.Workshops;
using Timberborn.Yielding;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class InventoryScriptableComponent : ScriptableComponentBase {

  const string InputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.InputGood";
  const string OutputGoodSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.OutputGood";
  const string HaulingModeSignalLocKey = "IgorZ.Automation.Scriptable.Inventory.Signal.HaulingMode";
  const string StartEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StartEmptyingStock";
  const string StopEmptyingStockActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.StopEmptyingStock";
  const string SetHaulingModeActionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.SetHaulingMode";
  const string EmptyingStatusDescriptionLocKey = "IgorZ.Automation.Scriptable.Inventory.Action.EmptyingStatus";

  const string InputGoodSignalNamePrefix = "Inventory.InputGood.";
  const string OutputGoodSignalNamePrefix = "Inventory.OutputGood.";
  internal const string HaulingModeSignalName = "Inventory.HaulingMode";
  internal const string StartEmptyingStockActionName = "Inventory.StartEmptying";
  internal const string StopEmptyingStockActionName = "Inventory.StopEmptying";
  internal const string SetHaulingModeActionName = "Inventory.SetHaulingMode";
  internal const string AcceptHaulingMode = "Accept";
  internal const string EmptyHaulingMode = "Empty";
  internal const string ObtainHaulingMode = "Obtain";
  internal const string SupplyHaulingMode = "Supply";

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
    var stockpilePriority = behavior.GetComponent<StockpilePriority>();
    if (inventory._goodDisallower is InRangeYielderGoodAllower) {
      // Yielders don't know which goods they allow until they find them the first time. Return all the allowed goods.
      return AddStockpilePrioritySignalIfAvailable(
          inventory.AllowedGoods.Select(x => MakeSignalName(x.StorableGood.GoodId, inventory)), stockpilePriority);
    }
    return AddStockpilePrioritySignalIfAvailable(
        GetSignalCapacities(behavior, inventory).Select(x => MakeSignalName(x.GoodId, inventory)), stockpilePriority);
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    if (name == HaulingModeSignalName) {
      var stockpilePriority = GetComponentOrThrow<StockpilePriority>(behavior);
      return () => ScriptValue.FromString(GetHaulingMode(stockpilePriority));
    }
    var parsed = ParseSignalName(name, behavior);
    var inventory = GetInventory(behavior);
    return () => GoodAmountSignal(parsed.goodId, inventory);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    if (name == HaulingModeSignalName) {
      GetComponentOrThrow<StockpilePriority>(behavior);  // Verify only.
      return HaulingModeSignalDef;
    }
    var parsed = ParseSignalName(name, behavior);
    return _signalDefCache.GetOrAdd(name, parsed.isInput, parsed.goodId, parsed.capacity, MakeSignalDef);
  }
  readonly ObjectsCache<SignalDef> _signalDefCache = new();

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var actionNames = new List<string>();
    if (behavior.GetComponent<Emptiable>()) {
      var inventory = GetInventory(behavior, throwIfNotFound: false);
      if (inventory.IsOutput && IsSafeOutputOnlyInventory(inventory)) {
        actionNames.Add(StartEmptyingStockActionName);
        actionNames.Add(StopEmptyingStockActionName);
      }
    }
    if (behavior.GetComponent<StockpilePriority>()) {
      actionNames.Add(SetHaulingModeActionName);
    }
    return actionNames.ToArray();
  }

  static bool IsSafeOutputOnlyInventory(Inventory inventory) {
    // Don't allow emptying buildings with inputs (ingredients) since the input will also be emptied (same as pause).
    foreach (var inputGood in inventory.InputGoods) {
      if (!inventory.OutputGoods.Contains(inputGood)) {
        return false;
      }
    }
    return true;
  }
  
  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    if (name == SetHaulingModeActionName) {
      var stockpilePriority = GetComponentOrThrow<StockpilePriority>(behavior);
      return args => SetHaulingModeAction(stockpilePriority, args);
    }
    var emptiable = GetComponentOrThrow<Emptiable>(behavior);
    return name switch {
        StartEmptyingStockActionName => _ => StartEmptyingStockAction(emptiable),
        StopEmptyingStockActionName => _ => StopEmptyingStockAction(emptiable),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    if (name == SetHaulingModeActionName) {
      GetComponentOrThrow<StockpilePriority>(behavior);  // Verify only.
      return SetHaulingModeActionDef;
    }
    GetComponentOrThrow<Emptiable>(behavior);  // Verify only.
    return name switch {
        StartEmptyingStockActionName => StartEmptyingStockActionDef,
        StopEmptyingStockActionName => StopEmptyingStockActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName == HaulingModeSignalName) {
      host.Behavior.GetOrCreate<StockpilePriorityChangeTracker>().AddSignal(signalOperator, host);
      return;
    }
    ParseSignalName(signalOperator.SignalName, host.Behavior, throwErrors: true);
    host.Behavior.GetOrCreate<InventoryChangeTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName == HaulingModeSignalName) {
      host.Behavior.GetOrThrow<StockpilePriorityChangeTracker>().RemoveSignal(signalOperator, host);
      return;
    }
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

  SignalDef MakeSignalDef(string name, bool isInput, string goodId, int capacity) {
    return new SignalDef {
        ScriptName = name,
        DisplayName = LocGoodSignal(isInput ? InputGoodSignalLocKey : OutputGoodSignalLocKey, goodId),
        Scope = SignalDef.ScopeEnum.Building,
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
            DisplayNumericFormatRange = (0, capacity),
        },
    };
  }

  SignalDef HaulingModeSignalDef => _haulingModeSignalDef ??= new SignalDef {
      ScriptName = HaulingModeSignalName,
      DisplayName = Loc.T(HaulingModeSignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = GetHaulingModeOptions(),
      },
  };
  SignalDef _haulingModeSignalDef;

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

  ActionDef SetHaulingModeActionDef => _setHaulingModeActionDef ??= new ActionDef {
      ScriptName = SetHaulingModeActionName,
      DisplayName = Loc.T(SetHaulingModeActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
              Options = GetHaulingModeOptions(),
          },
      ],
  };
  ActionDef _setHaulingModeActionDef;

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

  static void SetHaulingModeAction(StockpilePriority stockpilePriority, ScriptValue[] args) {
    AssertActionArgsCount(SetHaulingModeActionName, args, 1);
    SetHaulingModeAction(stockpilePriority, args[0].AsString);
  }

  static void SetHaulingModeAction(StockpilePriority stockpilePriority, string mode) {
    switch (mode) {
      case AcceptHaulingMode:
        stockpilePriority.Accept();
        break;
      case EmptyHaulingMode:
        stockpilePriority.Empty();
        break;
      case ObtainHaulingMode:
        stockpilePriority.Obtain();
        break;
      case SupplyHaulingMode:
        stockpilePriority.Supply();
        break;
      default:
        throw new ScriptError.ValueOutOfRange($"Unknown hauling mode: {mode}");
    }
  }

  #endregion

  #region Implementation

  readonly IGoodService _goodService;

  InventoryScriptableComponent(IGoodService goodService, BaseInstantiator instantiator) {
    _goodService = goodService;
  }

  static string[] AddStockpilePrioritySignalIfAvailable(IEnumerable<string> signals, StockpilePriority stockpilePriority) {
    return stockpilePriority
        ? signals.Append(HaulingModeSignalName).ToArray()
        : signals.ToArray();
  }

  static string MakeSignalName(string goodId, Inventory inventory) {
    var prefix = inventory.OutputGoods.Contains(goodId) ? OutputGoodSignalNamePrefix : InputGoodSignalNamePrefix;
    return prefix + SignalNameSegment.Encode(goodId);
  }

  static (bool isInput, string goodId, int capacity) ParseSignalName(
      string name, AutomationBehavior behavior, bool throwErrors = false) {
    var inventory = GetInventory(behavior);
    string goodId = null;
    var isInput = false;
    if (name.StartsWith(InputGoodSignalNamePrefix)) {
      isInput = true;
      goodId = ResolveGoodId(name[InputGoodSignalNamePrefix.Length..], inventory.InputGoods.Contains);
      if (!inventory.InputGoods.Contains(goodId)) {
        goodId = null;
      }
    } else if (name.StartsWith(OutputGoodSignalNamePrefix)) {
      goodId = ResolveGoodId(name[OutputGoodSignalNamePrefix.Length..], inventory.OutputGoods.Contains);
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
        : (isInput, goodId, GetSignalCapacity(behavior, inventory, goodId));
  }

  static List<GoodAmount> GetSignalCapacities(AutomationBehavior behavior, Inventory inventory) {
    List<GoodAmount> capacities = [];
    inventory.GetCapacity(capacities);
    if (capacities.Count > 0) {
      return capacities;
    }
    var currentRecipe = behavior.GetComponent<Manufactory>()?.CurrentRecipe;
    if (currentRecipe != null) {
      foreach (var ingredient in currentRecipe.Ingredients) {
        AddRecipeCapacity(capacities, inventory, currentRecipe, ingredient.ToGoodAmount());
      }
      foreach (var product in currentRecipe.Products) {
        AddRecipeCapacity(capacities, inventory, currentRecipe, product.ToGoodAmount());
      }
      if (currentRecipe.ConsumesFuel) {
        AddRecipeCapacity(capacities, inventory, currentRecipe.Fuel, currentRecipe.FuelCapacity);
      }
      return capacities;
    }
    var singleGoodAllower = behavior.GetComponent<SingleGoodAllower>();
    if (!behavior.GetComponent<StockpilePriority>() || singleGoodAllower?.AllowedGood == null) {
      return capacities;
    }
    var assignedGood = singleGoodAllower.AllowedGood;
    var allowedGood = inventory.AllowedGoods.FirstOrDefault(x => x.StorableGood.GoodId == assignedGood);
    AddRecipeCapacity(capacities, inventory, assignedGood, allowedGood.Amount);
    return capacities;
  }

  static void AddRecipeCapacity(
      List<GoodAmount> capacities, Inventory inventory, RecipeSpec currentRecipe, GoodAmount goodAmount) {
    AddRecipeCapacity(capacities, inventory, goodAmount.GoodId, currentRecipe.GetCapacity(goodAmount));
  }

  static void AddRecipeCapacity(List<GoodAmount> capacities, Inventory inventory, string goodId, int capacity) {
    if (inventory.InputGoods.Contains(goodId) || inventory.OutputGoods.Contains(goodId)) {
      capacities.Add(new GoodAmount(goodId, capacity));
    }
  }

  static int GetSignalCapacity(AutomationBehavior behavior, Inventory inventory, string goodId) {
    var capacity = inventory.LimitedAmount(goodId);
    if (capacity > 0) {
      return capacity;
    }
    return GetSignalCapacities(behavior, inventory).FirstOrDefault(x => x.GoodId == goodId).Amount;
  }

  static string ResolveGoodId(string signalNameSegment, Func<string, bool> hasGood) {
    if (hasGood(signalNameSegment)) {
      return signalNameSegment;
    }
    return SignalNameSegment.TryDecode(signalNameSegment, out var decodedGoodId) ? decodedGoodId : signalNameSegment;
  }

  string LocGoodSignal(string name, string goodId) {
    return Loc.T(name, _goodService.GetGood(goodId).PluralDisplayName.Value);
  }

  static string GetHaulingMode(StockpilePriority stockpilePriority) {
    if (stockpilePriority.IsEmptyActive) {
      return EmptyHaulingMode;
    }
    if (stockpilePriority.IsObtainActive) {
      return ObtainHaulingMode;
    }
    if (stockpilePriority.IsSupplyActive) {
      return SupplyHaulingMode;
    }
    return AcceptHaulingMode;
  }

  DropdownItem[] GetHaulingModeOptions() {
    return [
        (AcceptHaulingMode, Loc.T("StockpilePriority.Accept")),
        (EmptyHaulingMode, Loc.T("StockpilePriority.Empty")),
        (ObtainHaulingMode, Loc.T("StockpilePriority.Obtain")),
        (SupplyHaulingMode, Loc.T("StockpilePriority.Supply")),
    ];
  }

  /// <summary>Gets the storage inventory from the building. The construction site inventory is ignored.</summary>
  internal static Inventory GetInventory(BaseComponent building, bool throwIfNotFound = true) {
    var inventory = ComponentsAccessor.GetGoodsInventory(building);
    if (!inventory && throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Inventory component not found");
    }
    return inventory;
  }

  #endregion

  #region Inventory change tracker component

  internal sealed class InventoryChangeTracker : AbstractStatusTracker, IAwakableComponent {

    Inventory _inventory;

    public void Awake() {
      _inventory = GetInventory(AutomationBehavior, throwIfNotFound: false);
      if (!_inventory) {
        throw new InvalidOperationException("Inventory component not found on: " + DebugEx.ObjectToString(this));
      }
      _inventory.InventoryStockChanged += NotifyChange;
    }

    void NotifyChange(object sender, InventoryStockChangedEventArgs args) {
      TriggerSignalUpdate(MakeSignalName(args.GoodAmount.GoodId, _inventory));
    }
  }

  #endregion

  #region Stockpile priority change tracker component

  internal sealed class StockpilePriorityChangeTracker : AbstractStatusTracker, IAwakableComponent {

    public void Awake() {
      AutomationBehavior.GetComponentOrFail<StockpilePriorityChangeListener>().PriorityChanged += NotifyChange;
    }

    void NotifyChange(object sender, EventArgs args) {
      TriggerSignalUpdate(HaulingModeSignalName);
    }
  }

  #endregion

  #region Emptying status presenter

  /// <summary>
  /// Creates a custom status icon that indicates that the storage is being emptying. If the status is changed
  /// externally, then hides the status and notifies the action.
  /// </summary>
  internal sealed class EmptyingStatusBehavior : AbstractStatusTracker, IInitializableEntity {

    ILoc _loc;
    StatusToggle _statusToggle;
    Emptiable _emptiable;

    /// <inheritdoc/>
    public override bool AddAction(ActionOperator actionOperator) {
      if (!base.AddAction(actionOperator)) {
        return false;
      }
      if (_statusToggle != null) {  // On game load, actions can be added before InitializeEntity().
        RefreshStatus();
      }
      return true;
    }

    /// <inheritdoc/>
    public override bool RemoveAction(ActionOperator actionOperator) {
      if (base.RemoveAction(actionOperator)) {
        return true;
      }
      if (_emptiable.IsMarkedForEmptying) {
        _emptiable.UnmarkForEmptying();
      }
      return false;
    }

    [Inject]
    public void InjectDependencies(ILoc loc) {
      _loc = loc;
    }

    /// <inheritdoc/>
    public void InitializeEntity() {
      _emptiable = AutomationBehavior.GetComponentOrFail<Emptiable>();
      _emptiable.UnmarkedForEmptying += (_, _) => RefreshStatus();
      _emptiable.MarkedForEmptying += (_, _) => RefreshStatus();
      _statusToggle = StatusToggle.CreatePriorityStatusWithFloatingIcon(
          EmptyingStatusIcon, _loc.T(EmptyingStatusDescriptionLocKey));
      AutomationBehavior.GetComponentOrFail<StatusSubject>().RegisterStatus(_statusToggle);
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
