using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.AutomationSystemUI;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Localization;
using Timberborn.StockpilePrioritySystem;

namespace Automation.Tests;

static class RulesUIHelperTests {
  public static void ListsOnlyBuildingNumberSignalsForExport() {
    var scriptable = new TestScriptable("Test");
    scriptable.RegisterSignal("Test.BuildingNumber", ScriptValue.TypeEnum.Number);
    scriptable.RegisterSignal("Test.GlobalNumber", ScriptValue.TypeEnum.Number, scope: SignalDef.ScopeEnum.Global);
    scriptable.RegisterSignal("Test.BuildingString", ScriptValue.TypeEnum.String);
    var helper = CreateHelper(TestScripting.CreateService(scriptable));

    helper.SetBuilding(new AutomationBehavior());

    Assert.Equal(1, helper.BuildingSignalNames.Count);
    Assert.Equal("Test.BuildingNumber", helper.BuildingSignalNames[0]);
    Assert.Equal(1, helper.BuildingSignals.Count);
    Assert.Equal("Test.BuildingNumber", helper.BuildingSignals[0].SignalName);
  }

  public static void ListsInventoryOutputSignalForStockpilePriorityStorage() {
    var inventoryComponent = CreateInventoryComponent();
    var scriptingService = TestScripting.CreateService();
    scriptingService.RegisterScriptable(inventoryComponent);
    var helper = CreateHelper(scriptingService);
    var inventory = CreateInventory(inputGoods: ["Water"], outputGoods: ["Water"]);
    inventory.HideCapacity = true;
    var behavior = CreateBehavior(inventory);
    var singleGoodAllower = new SingleGoodAllower();
    singleGoodAllower.Allow("Water");
    behavior.SetComponent(singleGoodAllower);
    behavior.SetComponent(new StockpilePriority());

    helper.SetBuilding(behavior);

    Assert.Equal(1, helper.BuildingSignalNames.Count);
    Assert.Equal("Inventory.OutputGood.Water", helper.BuildingSignalNames[0]);
    Assert.Equal(1, helper.BuildingSignals.Count);
    Assert.Equal("Inventory.OutputGood.Water", helper.BuildingSignals[0].SignalName);
  }

  static RulesUIHelper CreateHelper(ScriptingService scriptingService) {
    var constructor = typeof(RulesUIHelper).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(ScriptingService), typeof(ILoc)],
        null);
    return (RulesUIHelper)constructor.Invoke([scriptingService, new TestLoc()]);
  }

  static InventoryScriptableComponent CreateInventoryComponent() {
    var constructor = typeof(InventoryScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IGoodService), typeof(BaseInstantiator)],
        null);
    var component = (InventoryScriptableComponent)constructor.Invoke([new TestGoodService(), null]);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static AutomationBehavior CreateBehavior(Inventory inventory) {
    var inventories = new Inventories();
    inventories.AddInventory(inventory);
    var behavior = new AutomationBehavior();
    behavior.SetComponent(inventory);
    behavior.SetComponent(inventories);
    return behavior;
  }

  static Inventory CreateInventory(string[] inputGoods, string[] outputGoods) {
    var inventory = new Inventory();
    foreach (var goodId in inputGoods) {
      inventory.AddInputGood(goodId, 30);
    }
    foreach (var goodId in outputGoods) {
      inventory.AddOutputGood(goodId, 300);
    }
    return inventory;
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
}
