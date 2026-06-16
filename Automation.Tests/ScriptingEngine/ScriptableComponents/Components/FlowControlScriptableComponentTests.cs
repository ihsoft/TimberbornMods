using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.WaterSourceSystem;

namespace Automation.Tests;

static class FlowControlScriptableComponentTests {
  public static void ExposesActionsForWaterSourceRegulator() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new WaterSourceRegulator());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("FlowControl.Open", actionNames[0]);
    Assert.Equal("FlowControl.Close", actionNames[1]);
  }

  public static void HidesActionsForMissingRegulator() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesOpenAndCloseActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var regulator = new WaterSourceRegulator();
    behavior.SetComponent(regulator);

    component.GetActionExecutor("FlowControl.Open", behavior)([]);

    Assert.True(regulator.IsOpen);

    component.GetActionExecutor("FlowControl.Close", behavior)([]);

    Assert.False(regulator.IsOpen);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();

    Assert.Throws<ScriptError.ParsingError>(
        () => component.GetActionDefinition("FlowControl.Missing", new AutomationBehavior()));
    Assert.Throws<ScriptError.BadStateError>(
        () => component.GetActionExecutor("FlowControl.Open", new AutomationBehavior()));
  }

  static FlowControlScriptableComponent CreateComponent() {
    var component = new FlowControlScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
