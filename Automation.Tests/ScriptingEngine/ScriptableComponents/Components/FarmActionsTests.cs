using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.EntitySystem;
using Timberborn.Fields;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.Planting;
using Timberborn.TemplateSystem;

namespace Automation.Tests;

static class FarmActionsTests {
  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) => key;
  }

  sealed class TestGoods : IGoodService {
    public GoodSpec GetGood(string id) => new() { Id = id, PluralDisplayName = new LocalizedText("Good." + id) };
    public GoodSpec GetGoodOrNull(string id) => GetGood(id);
  }

  /// <summary>Checks building-specific choices, priority reset, idempotence, and rejected resources.</summary>
  public static void PlantingPriority() {
    var component = Create<PlantableScriptableComponent>([null]);
    var carrot = new PlantableSpec { TemplateName = "Carrot" };
    carrot.SetComponent(new LabeledEntitySpec { DisplayNameLocKey = "Plant.Carrot" });
    var oak = new PlantableSpec { TemplateName = "Oak" };
    oak.SetComponent(new LabeledEntitySpec { DisplayNameLocKey = "Plant.Oak" });
    var first = new AutomationBehavior();
    var second = new AutomationBehavior();
    var firstPriority = new PlantablePrioritizer();
    var secondPriority = new PlantablePrioritizer();
    first.SetComponent(firstPriority);
    first.SetComponent(new PlanterBuilding { AllowedPlantables = [carrot] });
    second.SetComponent(secondPriority);
    second.SetComponent(new PlanterBuilding { AllowedPlantables = [oak] });

    var options = component.GetActionDefinition("Plantable.SetPriority", first).Arguments[0].Options;
    Assert.Equal(2, options.Length);
    Assert.Equal("", options[0].Value);
    Assert.Equal("Carrot", options[1].Value);
    Assert.Equal("Plant.Carrot", options[1].Text);
    Assert.Equal("Oak", component.GetActionDefinition("Plantable.SetPriority", second).Arguments[0].Options[1].Value);
    var execute = component.GetActionExecutor("Plantable.SetPriority", first);
    execute([ScriptValue.FromString("Carrot")]);
    execute([ScriptValue.FromString("Carrot")]);
    Assert.Equal(carrot, firstPriority.PrioritizedPlantableSpec);
    Assert.Equal(1, firstPriority.Changes);
    Assert.Equal(null, secondPriority.PrioritizedPlantableSpec);
    Assert.Throws<ScriptError.BadValue>(() => execute([ScriptValue.FromString("Oak")]));
    Assert.Equal(carrot, firstPriority.PrioritizedPlantableSpec);
    execute([ScriptValue.FromString("")]);
    Assert.Equal(null, firstPriority.PrioritizedPlantableSpec);
    Assert.Equal(2, firstPriority.Changes);
    CheckContract(component, "Plantable.SetPriority", first);
  }

  /// <summary>Checks template identity independently from the displayed good and from other buildings.</summary>
  public static void GatheringPriority() {
    var component = Create<CollectableScriptableComponent>([new TestGoods()]);
    var pine = new GatherableSpec {
        Yielder = new GatherableSpec.YieldSpec { Yield = new GoodAmountSpec { Id = "PineResin" } },
    };
    pine.SetComponent(new TemplateSpec { TemplateName = "Pine" });
    var first = new AutomationBehavior();
    var second = new AutomationBehavior();
    var firstPriority = new GatherablePrioritizer();
    var secondPriority = new GatherablePrioritizer();
    first.SetComponent(firstPriority);
    first.SetComponent(new GathererFlag { AllowedGatherables = [pine] });
    second.SetComponent(secondPriority);
    second.SetComponent(new GathererFlag());

    var options = component.GetActionDefinition("Collectable.SetPriority", first).Arguments[0].Options;
    Assert.Equal(2, options.Length);
    Assert.Equal("", options[0].Value);
    Assert.Equal("Pine", options[1].Value);
    Assert.Equal("Good.PineResin", options[1].Text);
    Assert.Equal(1, component.GetActionDefinition("Collectable.SetPriority", second).Arguments[0].Options.Length);
    var execute = component.GetActionExecutor("Collectable.SetPriority", first);
    execute([ScriptValue.FromString("Pine")]);
    execute([ScriptValue.FromString("Pine")]);
    Assert.Equal(pine, firstPriority.PrioritizedGatherable);
    Assert.Equal(1, firstPriority.Changes);
    Assert.Equal(null, secondPriority.PrioritizedGatherable);
    Assert.Throws<ScriptError.BadValue>(() => execute([ScriptValue.FromString("PineResin")]));
    Assert.Equal(pine, firstPriority.PrioritizedGatherable);
    execute([ScriptValue.FromString("")]);
    Assert.Equal(null, firstPriority.PrioritizedGatherable);
    Assert.Equal(2, firstPriority.Changes);
    CheckContract(component, "Collectable.SetPriority", first);
  }

  /// <summary>Checks both work modes and rejects invalid input without changing the current mode.</summary>
  public static void FarmMode() {
    var component = Create<FarmHouseScriptableComponent>([]);
    var first = new AutomationBehavior();
    var second = new AutomationBehavior();
    var firstFarm = new FarmHouse();
    var secondFarm = new FarmHouse();
    first.SetComponent(firstFarm);
    second.SetComponent(secondFarm);
    var options = component.GetActionDefinition("FarmHouse.SetMode", first).Arguments[0].Options;
    Assert.Equal(2, options.Length);
    Assert.Equal("Planting", options[0].Value);
    Assert.Equal("Harvesting", options[1].Value);
    var execute = component.GetActionExecutor("FarmHouse.SetMode", first);
    execute([ScriptValue.FromString("Harvesting")]);
    Assert.False(firstFarm.PlantingPrioritized);
    Assert.True(secondFarm.PlantingPrioritized);
    Assert.Throws<ScriptError.BadValue>(() => execute([ScriptValue.FromString("Invalid")]));
    Assert.False(firstFarm.PlantingPrioritized);
    execute([ScriptValue.FromString("Planting")]);
    Assert.True(firstFarm.PlantingPrioritized);
    CheckContract(component, "FarmHouse.SetMode", first);
  }

  static void CheckContract(ScriptableComponentBase component, string actionName, AutomationBehavior behavior) {
    Assert.Equal(actionName, component.GetActionNamesForBuilding(behavior)[0]);
    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionExecutor("Missing", behavior));
    Assert.Throws<ScriptError.BadStateError>(() => component.GetActionDefinition(actionName, new AutomationBehavior()));
    var execute = component.GetActionExecutor(actionName, behavior);
    Assert.Throws<ScriptError.ParsingError>(() => execute([]));
    Assert.Throws<ScriptError.ParsingError>(() => execute([ScriptValue.FromString(""), ScriptValue.FromString("")]));
    var option = component.GetActionDefinition(actionName, behavior).Arguments[0].Options[0].Value;
    var action = ActionOperator.Create(
        new ExpressionContext { ScriptHost = behavior }, actionName, [ConstantValueExpr.CreateStringLiteral(option)]);
    Assert.True(new GameAutomationConflictDetector().IsBuildingStateChangingAction(action));
    Assert.False(new GameAutomationConflictDetector().IsConflictingRule(behavior, action));
  }

  static T Create<T>(object[] args) where T : ScriptableComponentBase {
    var component = (T)Activator.CreateInstance(
        typeof(T), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }
}
