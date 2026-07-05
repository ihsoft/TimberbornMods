using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.AutomationBuildings;
using Timberborn.Localization;

namespace Automation.Tests;

static class GateScriptableComponentTests {
  public static void ExposesActionForGate() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Gate());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Gate.SetState", actionNames[0]);
  }

  public static void HidesActionForMissingGate() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesSetStateAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var gate = new Gate();
    behavior.SetComponent(gate);
    var executor = component.GetActionExecutor("Gate.SetState", behavior);

    executor([ScriptValue.FromString("open")]);

    Assert.True(gate.OpenMode);

    executor([ScriptValue.FromString("closed")]);

    Assert.True(gate.ClosedMode);

    executor([ScriptValue.FromString("automated")]);

    Assert.True(gate.AutomatedMode);
  }

  public static void BuildsActionDefinition() {
    var component = CreateComponent();

    var actionDef = component.GetActionDefinition("Gate.SetState", new AutomationBehavior());

    Assert.Equal("Gate.SetState", actionDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Gate.Action.SetState", actionDef.DisplayName);
    Assert.Equal(3, actionDef.Arguments[0].Options.Length);
    Assert.Equal("open", actionDef.Arguments[0].Options[0].Value);
    Assert.Equal("closed", actionDef.Arguments[0].Options[1].Value);
    Assert.Equal("automated", actionDef.Arguments[0].Options[2].Value);
  }

  public static void RejectsInvalidActionArguments() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Gate());
    var executor = component.GetActionExecutor("Gate.SetState", behavior);

    Assert.Throws<ScriptError.ParsingError>(() => executor([]));
    Assert.Throws<System.InvalidOperationException>(() => executor([ScriptValue.FromString("bad")]));
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Gate());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Gate.Missing", behavior));
  }

  static GateScriptableComponent CreateComponent() {
    var component = new GateScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
