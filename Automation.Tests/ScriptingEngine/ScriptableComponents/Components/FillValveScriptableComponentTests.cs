using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class FillValveScriptableComponentTests {
  public static void ExposesActionsForFillValve() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("FillValve.Open", actionNames[0]);
    Assert.Equal("FillValve.Close", actionNames[1]);
    Assert.Equal("FillValve.SetHeight", actionNames[2]);
  }

  public static void HidesActionsForMissingFillValve() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ExecutesOpenCloseAndSetHeightActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var fillValve = new FillValve { MinTargetHeight = 4, MaxTargetHeight = 7 };
    behavior.SetComponent(fillValve);

    component.GetActionExecutor("FillValve.Open", behavior)([]);

    Assert.True(fillValve.TargetHeightEnabled);
    Assert.Equal(7f, fillValve.TargetHeight);

    component.GetActionExecutor("FillValve.Close", behavior)([]);

    Assert.True(fillValve.TargetHeightEnabled);
    Assert.Equal(4f, fillValve.TargetHeight);

    component.GetActionExecutor("FillValve.SetHeight", behavior)([ScriptValue.FromFloat(1.25f)]);

    Assert.True(fillValve.TargetHeightEnabled);
    Assert.Equal(5.25f, fillValve.TargetHeight);
    Assert.Equal(3, fillValve.SetTargetHeightCalls);
    Assert.Equal(3, fillValve.SetTargetHeightEnabledCalls);
  }

  public static void BuildsActionDefinitionsForTargetDepth() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var fillValve = new FillValve { MinTargetHeight = 2, MaxTargetHeight = 5 };
    behavior.SetComponent(fillValve);

    var openDef = component.GetActionDefinition("FillValve.Open", behavior);
    var closeDef = component.GetActionDefinition("FillValve.Close", behavior);
    var setHeightDef = component.GetActionDefinition("FillValve.SetHeight", behavior);

    Assert.Equal("FillValve.Open", openDef.ScriptName);
    Assert.Equal(0, openDef.Arguments.Length);
    Assert.Equal("FillValve.Close", closeDef.ScriptName);
    Assert.Equal(0, closeDef.Arguments.Length);
    Assert.Equal("FillValve.SetHeight", setHeightDef.ScriptName);
    Assert.Equal((0, 3f), setHeightDef.Arguments[0].DisplayNumericFormatRange);
    setHeightDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(3));
    Assert.Throws<ScriptError.ValueOutOfRange>(
        () => setHeightDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(3.01f)));
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("FillValve.Missing", behavior));
  }

  public static FillValveScriptableComponent CreateComponent() {
    var component = new FillValveScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
