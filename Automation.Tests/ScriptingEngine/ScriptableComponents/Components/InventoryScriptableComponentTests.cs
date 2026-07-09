using System;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BlockSystem;
using Timberborn.Emptying;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.StatusSystem;
using Timberborn.StockpilePrioritySystem;
using Timberborn.Workshops;

namespace Automation.Tests;

static class InventoryScriptableComponentTests {
  public static void ExposesSignalsForInventory() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("Inventory.InputGood.Log", signalNames[0]);
    Assert.Equal("Inventory.OutputGood.Plank", signalNames[1]);
  }

  public static void EncodesUnsafeGoodIds() {
    var component = CreateComponent();
    var inventory = CreateInventory(inputGoods: ["Log_Rewrite"], outputGoods: ["123Food"]);
    inventory.SetAmount("Log_Rewrite", 17);
    inventory.SetAmount("123Food", 8);
    var behavior = CreateBehavior(inventory);

    var signalNames = component.GetSignalNamesForBuilding(behavior);
    var inputDef = component.GetSignalDefinition("Inventory.InputGood.LogX5FRewrite", behavior);
    var outputDef = component.GetSignalDefinition("Inventory.OutputGood.X3123Food", behavior);

    Assert.Equal("Inventory.InputGood.LogX5FRewrite", signalNames[0]);
    Assert.Equal("Inventory.OutputGood.X3123Food", signalNames[1]);
    Assert.Equal(17, component.GetSignalSource("Inventory.InputGood.LogX5FRewrite", behavior)().AsInt);
    Assert.Equal(8, component.GetSignalSource("Inventory.OutputGood.X3123Food", behavior)().AsInt);
    Assert.Equal("Inventory.InputGood.LogX5FRewrite", inputDef.ScriptName);
    Assert.Equal("Inventory.OutputGood.X3123Food", outputDef.ScriptName);
  }

  public static void ExposesAllowedGoodsSignalsForYielderInventory() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(isYielderInventory: true));

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("Inventory.InputGood.Log", signalNames[0]);
    Assert.Equal("Inventory.OutputGood.Plank", signalNames[1]);
  }

  public static void ExposesHaulingModeSignalForStockpilePriority() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());
    behavior.SetComponent(new StockpilePriority());

    var signalNames = component.GetSignalNamesForBuilding(behavior);
    var signalDef = component.GetSignalDefinition("Inventory.HaulingMode", behavior);

    Assert.Equal("Inventory.HaulingMode", signalNames[2]);
    Assert.Equal("Inventory.HaulingMode", signalDef.ScriptName);
    Assert.Equal("Accept", signalDef.Result.Options[0].Value);
    Assert.Equal("Supply", signalDef.Result.Options[3].Value);
  }

  public static void ExposesCurrentRecipeSignalsWhenInventoryLimitsAreMissing() {
    var component = CreateComponent();
    var inventory = CreateInventory(
        inputGoods: ["Plank", "PineResin"], outputGoods: ["TreatedPlank"], capacity: 0);
    var behavior = CreateBehavior(inventory);
    behavior.SetComponent(new Manufactory {
        CurrentRecipe = new RecipeSpec {
            Ingredients = [
                new GoodAmountSpec { Id = "Plank", Amount = 1 },
                new GoodAmountSpec { Id = "PineResin", Amount = 1 },
            ],
            Products = [
                new GoodAmountSpec { Id = "TreatedPlank", Amount = 1 },
            ],
            CyclesCapacity = 10,
        },
    });

    var signalNames = component.GetSignalNamesForBuilding(behavior);
    var outputDef = component.GetSignalDefinition("Inventory.OutputGood.TreatedPlank", behavior);

    Assert.Equal("Inventory.InputGood.Plank", signalNames[0]);
    Assert.Equal("Inventory.InputGood.PineResin", signalNames[1]);
    Assert.Equal("Inventory.OutputGood.TreatedPlank", signalNames[2]);
    Assert.Equal((0, 10), outputDef.Result.DisplayNumericFormatRange);
  }

  public static void HidesSignalsForMissingInventory() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetSignalNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ReadsInventoryAmounts() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());

    Assert.Equal(12, component.GetSignalSource("Inventory.InputGood.Log", behavior)().AsInt);
    Assert.Equal(4, component.GetSignalSource("Inventory.OutputGood.Plank", behavior)().AsInt);
  }

  public static void ReadsHaulingModeSignal() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());
    var stockpilePriority = new StockpilePriority();
    behavior.SetComponent(stockpilePriority);

    Assert.Equal("Accept", component.GetSignalSource("Inventory.HaulingMode", behavior)().AsString);

    stockpilePriority.Obtain();

    Assert.Equal("Obtain", component.GetSignalSource("Inventory.HaulingMode", behavior)().AsString);
  }

  public static void BuildsSignalDefinitions() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());

    var inputDef = component.GetSignalDefinition("Inventory.InputGood.Log", behavior);
    var outputDef = component.GetSignalDefinition("Inventory.OutputGood.Plank", behavior);

    Assert.Equal("Inventory.InputGood.Log", inputDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Inventory.Signal.InputGood", inputDef.DisplayName);
    Assert.Equal((0, 30), inputDef.Result.DisplayNumericFormatRange);
    Assert.Equal("Inventory.OutputGood.Plank", outputDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Inventory.Signal.OutputGood", outputDef.DisplayName);
    Assert.Equal((0, 20), outputDef.Result.DisplayNumericFormatRange);
  }

  public static void ExposesEmptyingActionsOnlyForSafeOutputInventory() {
    var component = CreateComponent();
    var outputBehavior = CreateBehavior(CreateInventory(inputGoods: [], outputGoods: ["Plank"]));
    outputBehavior.SetComponent(new Emptiable());
    var mixedBehavior = CreateBehavior(CreateInventory(inputGoods: ["Log"], outputGoods: ["Plank"]));
    mixedBehavior.SetComponent(new Emptiable());

    var actionNames = component.GetActionNamesForBuilding(outputBehavior);

    Assert.Equal("Inventory.StartEmptying", actionNames[0]);
    Assert.Equal("Inventory.StopEmptying", actionNames[1]);
    Assert.Equal(0, component.GetActionNamesForBuilding(mixedBehavior).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(CreateBehavior(CreateInventory())).Length);
  }

  public static void ExposesSetHaulingModeActionForStockpilePriority() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(inputGoods: ["Log"], outputGoods: ["Plank"]));
    behavior.SetComponent(new StockpilePriority());

    var actionNames = component.GetActionNamesForBuilding(behavior);
    var actionDef = component.GetActionDefinition("Inventory.SetHaulingMode", behavior);

    Assert.Equal(1, actionNames.Length);
    Assert.Equal("Inventory.SetHaulingMode", actionNames[0]);
    Assert.Equal("Inventory.SetHaulingMode", actionDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Inventory.Action.SetHaulingMode", actionDef.DisplayName);
    Assert.Equal("Empty", actionDef.Arguments[0].Options[1].Value);
    Assert.Equal("Obtain", actionDef.Arguments[0].Options[2].Value);
  }

  public static void ExposesEmptyingAndHaulingModeActionsForIndependentComponents() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(inputGoods: [], outputGoods: ["Plank"]));
    behavior.SetComponent(new Emptiable());
    behavior.SetComponent(new StockpilePriority());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal(3, actionNames.Length);
    Assert.Equal("Inventory.StartEmptying", actionNames[0]);
    Assert.Equal("Inventory.StopEmptying", actionNames[1]);
    Assert.Equal("Inventory.SetHaulingMode", actionNames[2]);
    Assert.Equal("Inventory.StartEmptying", component.GetActionDefinition("Inventory.StartEmptying", behavior).ScriptName);
    Assert.Equal("Inventory.SetHaulingMode", component.GetActionDefinition("Inventory.SetHaulingMode", behavior).ScriptName);
  }

  public static void ExecutesEmptyingActions() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(inputGoods: [], outputGoods: ["Plank"]));
    var emptiable = new Emptiable();
    behavior.SetComponent(emptiable);

    component.GetActionExecutor("Inventory.StartEmptying", behavior)([]);

    Assert.True(emptiable.IsMarkedForEmptying);
    Assert.Equal(1, emptiable.MarkForEmptyingCalls);

    component.GetActionExecutor("Inventory.StartEmptying", behavior)([]);

    Assert.Equal(1, emptiable.MarkForEmptyingCalls);

    component.GetActionExecutor("Inventory.StopEmptying", behavior)([]);

    Assert.False(emptiable.IsMarkedForEmptying);
    Assert.Equal(1, emptiable.UnmarkForEmptyingCalls);
  }

  public static void ExecutesSetHaulingModeAction() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());
    var stockpilePriority = new StockpilePriority();
    behavior.SetComponent(stockpilePriority);

    component.GetActionExecutor("Inventory.SetHaulingMode", behavior)([ScriptValue.FromString("Supply")]);

    Assert.True(stockpilePriority.IsSupplyActive);

    component.GetActionExecutor("Inventory.SetHaulingMode", behavior)([ScriptValue.FromString("Accept")]);

    Assert.True(stockpilePriority.IsAcceptActive);
  }

  public static void ExecutesEmptyingActionsThroughEmptiableWhenHaulingModeExists() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(inputGoods: [], outputGoods: ["Plank"]));
    var emptiable = new Emptiable();
    var stockpilePriority = new StockpilePriority();
    behavior.SetComponent(emptiable);
    behavior.SetComponent(stockpilePriority);

    component.GetActionExecutor("Inventory.StartEmptying", behavior)([]);

    Assert.True(emptiable.IsMarkedForEmptying);
    Assert.True(stockpilePriority.IsAcceptActive);

    component.GetActionExecutor("Inventory.StopEmptying", behavior)([]);

    Assert.False(emptiable.IsMarkedForEmptying);
    Assert.True(stockpilePriority.IsAcceptActive);
  }

  public static void NotifiesInventorySignalListeners() {
    var component = CreateComponent();
    var inventory = CreateInventory();
    var behavior = CreateBehavior(inventory, withDynamicComponents: true);
    var listener = new TestSignalListener(behavior);

    component.RegisterSignalChangeCallback(Signal("Inventory.InputGood.Log", behavior), listener);
    inventory.SetAmount("Log", 13);

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Inventory.InputGood.Log", listener.LastSignalName);
  }

  public static void NotifiesHaulingModeSignalListeners() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(), withDynamicComponents: true);
    behavior.SetComponent(new StockpilePriority());
    var listenerComponent = new StockpilePriorityChangeListener();
    behavior.SetComponent(listenerComponent);
    var listener = new TestSignalListener(behavior);

    component.RegisterSignalChangeCallback(Signal("Inventory.HaulingMode", behavior), listener);
    behavior.GetComponent<StockpilePriority>().Supply();
    listenerComponent.RaisePriorityChanged();

    Assert.Equal(1, listener.Calls);
    Assert.Equal("Inventory.HaulingMode", listener.LastSignalName);
  }

  public static void InstallsEmptyingStatusAction() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory(inputGoods: [], outputGoods: ["Plank"]), withDynamicComponents: true);
    behavior.SetComponent(new Emptiable());
    behavior.SetComponent(new StatusSubject());
    var action = Action("Inventory.StartEmptying", behavior);

    behavior.InitializeEntity();
    component.InstallAction(action, behavior);

    Assert.True(behavior.GetOrThrow<InventoryScriptableComponent.EmptyingStatusBehavior>().HasActions);
    Assert.False(behavior.GetComponent<StatusSubject>().RegisteredStatuses[0].IsActive);
  }

  public static void ReportsUnknownSignalAndAction() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateInventory());
    behavior.SetComponent(new Emptiable());

    Assert.Throws<ScriptError.BadStateError>(
        () => component.GetSignalDefinition("Inventory.InputGood.Plank", behavior));
    Assert.Throws<ScriptError.BadStateError>(
        () => component.GetSignalDefinition("Inventory.HaulingMode", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Inventory.Missing", behavior));
  }

  static InventoryScriptableComponent CreateComponent() {
    var constructor = typeof(InventoryScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IGoodService), typeof(Bindito.Core.BaseInstantiator)],
        null);
    var component = (InventoryScriptableComponent)constructor.Invoke([new TestGoodService(), null]);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static AutomationBehavior CreateBehavior(Inventory inventory, bool withDynamicComponents = false) {
    if (withDynamicComponents) {
      var container = new TestContainer();
      container.Register<ILoc>(() => new TestLoc());
      SetDependencyContainer(container);
    }
    var behavior = new AutomationBehavior();
    var inventories = new Inventories();
    inventories.AddInventory(inventory);
    behavior.SetComponent(inventories);
    if (withDynamicComponents) {
      behavior.SetComponent(new BlockObject());
      behavior.InjectDependencies(new AutomationService());
      behavior.Awake();
    }
    return behavior;
  }

  static Inventory CreateInventory(
      string[] inputGoods = null, string[] outputGoods = null, bool isYielderInventory = false, int? capacity = null) {
    inputGoods ??= ["Log"];
    outputGoods ??= ["Plank"];
    var inventory = new Inventory(isYielderInventory);
    foreach (var goodId in inputGoods) {
      inventory.AddInputGood(goodId, capacity ?? (goodId == "Log" ? 30 : 10));
    }
    foreach (var goodId in outputGoods) {
      inventory.AddOutputGood(goodId, capacity ?? (goodId == "Plank" ? 20 : 10));
    }
    inventory.SetAmount("Log", 12);
    inventory.SetAmount("Plank", 4);
    return inventory;
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static ActionOperator Action(string actionName, AutomationBehavior behavior) {
    return ActionOperator.Create(new ExpressionContext { ScriptHost = behavior }, actionName, []);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class TestGoodService : IGoodService {
    public GoodSpec GetGood(string id) {
      return new GoodSpec {
          Id = id,
          PluralDisplayName = new LocalizedText(id + "s"),
      };
    }

    public GoodSpec GetGoodOrNull(string id) {
      return GetGood(id);
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }
}
