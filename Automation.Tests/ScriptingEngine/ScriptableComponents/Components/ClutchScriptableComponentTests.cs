using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.PowerManagement;

namespace Automation.Tests;

static class ClutchScriptableComponentTests {
  public static void ExposesActionsForClutch() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Clutch());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Clutch.Engage", actionNames[0]);
    Assert.Equal("Clutch.Disengage", actionNames[1]);
  }

  public static void HidesActionsForMissingClutch() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesEngageAndDisengageActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var clutch = new Clutch();
    behavior.SetComponent(clutch);

    component.GetActionExecutor("Clutch.Engage", behavior)([]);

    Assert.Equal(ClutchMode.Engaged, clutch.Mode);

    component.GetActionExecutor("Clutch.Disengage", behavior)([]);

    Assert.Equal(ClutchMode.Disengaged, clutch.Mode);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();

    Assert.Throws<ScriptError.ParsingError>(
        () => component.GetActionDefinition("Clutch.Missing", new AutomationBehavior()));
    Assert.Throws<ScriptError.BadStateError>(
        () => component.GetActionExecutor("Clutch.Engage", new AutomationBehavior()));
  }

  static ClutchScriptableComponent CreateComponent() {
    var component = new ClutchScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
