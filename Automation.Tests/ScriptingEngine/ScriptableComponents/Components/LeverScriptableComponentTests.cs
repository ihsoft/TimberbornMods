using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.AutomationBuildings;
using Timberborn.Localization;

namespace Automation.Tests;

static class LeverScriptableComponentTests {
  public static void ExposesActionForLever() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Lever());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Lever.SetState", actionNames[0]);
  }

  public static void HidesActionForMissingLever() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesSetStateAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var lever = new Lever();
    behavior.SetComponent(lever);
    var executor = component.GetActionExecutor("Lever.SetState", behavior);

    executor([ScriptValue.FromString("on")]);

    Assert.True(lever.IsOn);

    executor([ScriptValue.FromString("off")]);

    Assert.False(lever.IsOn);
  }

  public static void RejectsInvalidActionArguments() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Lever());
    var executor = component.GetActionExecutor("Lever.SetState", behavior);

    Assert.Throws<ScriptError.ParsingError>(() => executor([]));
    Assert.Throws<System.InvalidOperationException>(() => executor([ScriptValue.FromString("bad")]));
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Lever());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Lever.Missing", behavior));
  }

  static LeverScriptableComponent CreateComponent() {
    var component = new LeverScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
