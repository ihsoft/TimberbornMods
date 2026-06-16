using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Explosions;
using Timberborn.Localization;
using UnityEngine;

namespace Automation.Tests;

static class DynamiteScriptableComponentTests {
  public static void ExposesActionsForDynamite() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Dynamite.Detonate", actionNames[0]);
    Assert.Equal("Dynamite.DetonateAndRepeat", actionNames[1]);
  }

  public static void HidesActionsForMissingDynamite() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void BuildsActionDefinitions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    var detonateDef = component.GetActionDefinition("Dynamite.Detonate", behavior);
    var repeatDef = component.GetActionDefinition("Dynamite.DetonateAndRepeat", behavior);

    Assert.Equal("Dynamite.Detonate", detonateDef.ScriptName);
    Assert.Equal(0, detonateDef.Arguments.Length);
    Assert.Equal("Dynamite.DetonateAndRepeat", repeatDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, repeatDef.Arguments[0].ValueType);
    Assert.Equal((1, 6), repeatDef.Arguments[0].DisplayNumericFormatRange);
  }

  public static void ValidatesRepeatCount() {
    var component = CreateComponent();
    var repeatDef = component.GetActionDefinition("Dynamite.DetonateAndRepeat", new AutomationBehavior());

    repeatDef.Arguments[0].ArgumentValidator(ConstantValueExpr.CreateFromValue(ScriptValue.FromInt(2)));

    Assert.Throws<ScriptError.ParsingError>(
        () => repeatDef.Arguments[0].ArgumentValidator(new TestNonConstantValueExpr()));
    Assert.Throws<ScriptError.ParsingError>(
        () => repeatDef.Arguments[0].ArgumentValidator(
            ConstantValueExpr.CreateFromValue(ScriptValue.FromString("2"))));
  }

  public static void ExecutesDetonateActionByQueueingCoroutine() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());
    var action = Action("Dynamite.Detonate", behavior);
    var beforeCount = MonoBehaviour.QueuedCoroutineCount;

    component.InstallAction(action, behavior);
    action.Execute();

    Assert.Equal(beforeCount + 1, MonoBehaviour.QueuedCoroutineCount);
    MonoBehaviour.ClearQueuedCoroutines();
  }

  public static void InstallsAndUninstallsActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());
    var action = Action("Dynamite.Detonate", behavior);

    component.InstallAction(action, behavior);
    Assert.True(behavior.TryGetDynamicComponent<DynamiteScriptableComponent.DynamiteStateController>(out _));

    component.UninstallAction(action, behavior);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionExecutor("Dynamite.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Dynamite.Missing", behavior));
  }

  static DynamiteScriptableComponent CreateComponent() {
    var component = new DynamiteScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static ActionOperator Action(string actionName, AutomationBehavior behavior) {
    return ActionOperator.Create(new ExpressionContext { ScriptHost = behavior }, actionName, []);
  }

  sealed class TestNonConstantValueExpr : IValueExpr {
    public ScriptValue.TypeEnum ValueType => ScriptValue.TypeEnum.Number;
    public System.Func<ScriptValue> ValueFn => () => ScriptValue.FromInt(2);

    public void VisitNodes(System.Action<IExpression> visitorFn) {
      visitorFn(this);
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
