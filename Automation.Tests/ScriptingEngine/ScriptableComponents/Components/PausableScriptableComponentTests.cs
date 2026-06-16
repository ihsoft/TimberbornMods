using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Buildings;
using Timberborn.Localization;

namespace Automation.Tests;

static class PausableScriptableComponentTests {
  public static void ExposesActionsForPausableBuilding() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new PausableBuilding());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Pausable.Pause", actionNames[0]);
    Assert.Equal("Pausable.Unpause", actionNames[1]);
  }

  public static void HidesActionsForNonPausableBuilding() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new PausableBuilding { Pausable = false });

    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesPauseAndResumeActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var building = new PausableBuilding();
    behavior.SetComponent(building);

    component.GetActionExecutor("Pausable.Pause", behavior)([]);

    Assert.True(building.Paused);

    component.GetActionExecutor("Pausable.Unpause", behavior)([]);

    Assert.False(building.Paused);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new PausableBuilding());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Pausable.Missing", behavior));
  }

  static PausableScriptableComponent CreateComponent() {
    var component = new PausableScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
