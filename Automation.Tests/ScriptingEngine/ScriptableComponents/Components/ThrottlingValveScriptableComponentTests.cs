using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class ThrottlingValveScriptableComponentTests {
  public static void ExposesActionsForThrottlingValve() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new ThrottlingValve());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("ThrottlingValve.Open", actionNames[0]);
    Assert.Equal("ThrottlingValve.Close", actionNames[1]);
    Assert.Equal("ThrottlingValve.SetFlow", actionNames[2]);
  }

  public static void HidesActionsForMissingThrottlingValve() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesOpenCloseAndSetFlowActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var valve = new ThrottlingValve { MaxOutflowLimit = 2 };
    behavior.SetComponent(valve);

    component.GetActionExecutor("ThrottlingValve.Open", behavior)([]);

    Assert.False(valve.OutflowLimitEnabled);
    Assert.Equal(2f, valve.OutflowLimit);

    component.GetActionExecutor("ThrottlingValve.Close", behavior)([]);

    Assert.True(valve.OutflowLimitEnabled);
    Assert.Equal(0f, valve.OutflowLimit);

    component.GetActionExecutor("ThrottlingValve.SetFlow", behavior)([ScriptValue.FromFloat(1.25f)]);

    Assert.True(valve.OutflowLimitEnabled);
    Assert.Equal(1.25f, valve.OutflowLimit);
    Assert.Equal(3, valve.SetOutflowLimitCalls);
    Assert.Equal(3, valve.SetOutflowLimitEnabledCalls);
  }

  public static void BuildsActionDefinitionsForOutflowLimit() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var valve = new ThrottlingValve { MaxOutflowLimit = 2 };
    behavior.SetComponent(valve);

    var openDef = component.GetActionDefinition("ThrottlingValve.Open", behavior);
    var closeDef = component.GetActionDefinition("ThrottlingValve.Close", behavior);
    var setFlowDef = component.GetActionDefinition("ThrottlingValve.SetFlow", behavior);

    Assert.Equal("ThrottlingValve.Open", openDef.ScriptName);
    Assert.Equal(0, openDef.Arguments.Length);
    Assert.Equal("ThrottlingValve.Close", closeDef.ScriptName);
    Assert.Equal(0, closeDef.Arguments.Length);
    Assert.Equal("ThrottlingValve.SetFlow", setFlowDef.ScriptName);
    Assert.Equal((0, 2f), setFlowDef.Arguments[0].DisplayNumericFormatRange);
    setFlowDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(2));
    Assert.Throws<ScriptError.ValueOutOfRange>(
        () => setFlowDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(2.01f)));
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new ThrottlingValve());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("ThrottlingValve.Missing", behavior));
  }

  static ThrottlingValveScriptableComponent CreateComponent() {
    var component = new ThrottlingValveScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
