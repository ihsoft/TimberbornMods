using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Hauling;
using Timberborn.Localization;

namespace Automation.Tests;

static class PrioritizableScriptableComponentTests {
  public static void ExposesActionsForPrioritizableBuilding() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new HaulPrioritizable());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Prioritizable.SetHaulers", actionNames[0]);
    Assert.Equal("Prioritizable.ResetHaulers", actionNames[1]);
  }

  public static void HidesActionsForMissingPrioritizableComponent() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesSetAndResetHaulersActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var prioritizable = new HaulPrioritizable();
    behavior.SetComponent(prioritizable);

    component.GetActionExecutor("Prioritizable.SetHaulers", behavior)([]);

    Assert.True(prioritizable.Prioritized);

    component.GetActionExecutor("Prioritizable.ResetHaulers", behavior)([]);

    Assert.False(prioritizable.Prioritized);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new HaulPrioritizable());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Prioritizable.Missing", behavior));
  }

  static PrioritizableScriptableComponent CreateComponent() {
    var component = new PrioritizableScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
