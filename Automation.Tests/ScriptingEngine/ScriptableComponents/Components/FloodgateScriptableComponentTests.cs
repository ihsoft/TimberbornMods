using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class FloodgateScriptableComponentTests {
  public static void ExposesSignalAndActionForFloodgate() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Floodgate());

    Assert.Equal("Floodgate.Height", component.GetSignalNamesForBuilding(behavior)[0]);
    Assert.Equal("Floodgate.SetHeight", component.GetActionNamesForBuilding(behavior)[0]);
  }

  public static void HidesSignalAndActionForMissingFloodgate() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void ReadsAndSetsFloodgateHeight() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var floodgate = new Floodgate { Height = 1.25f, MaxHeight = 3 };
    behavior.SetComponent(floodgate);

    Assert.Equal(125, component.GetSignalSource("Floodgate.Height", behavior)().AsRawNumber);

    component.GetActionExecutor("Floodgate.SetHeight", behavior)([ScriptValue.FromFloat(2.5f)]);

    Assert.Equal(2.5f, floodgate.Height);
    Assert.Equal(1, floodgate.SetHeightCalls);
  }

  public static void DoesNotSetSameHeightAgain() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var floodgate = new Floodgate { Height = 1.25f, MaxHeight = 3 };
    behavior.SetComponent(floodgate);

    component.GetActionExecutor("Floodgate.SetHeight", behavior)([ScriptValue.FromFloat(1.25f)]);

    Assert.Equal(0, floodgate.SetHeightCalls);
  }

  public static void BuildsDefinitionsForFloodgateHeight() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var floodgate = new Floodgate { MaxHeight = 3 };
    behavior.SetComponent(floodgate);

    var signalDef = component.GetSignalDefinition("Floodgate.Height", behavior);
    var actionDef = component.GetActionDefinition("Floodgate.SetHeight", behavior);

    Assert.Equal("Floodgate.Height", signalDef.ScriptName);
    Assert.Equal((0, 3), signalDef.Result.DisplayNumericFormatRange);
    Assert.Equal("Floodgate.SetHeight", actionDef.ScriptName);
    Assert.Equal((0, 3), actionDef.Arguments[0].DisplayNumericFormatRange);
    actionDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(3));
    Assert.Throws<ScriptError.ValueOutOfRange>(
        () => actionDef.Arguments[0].RuntimeValueValidator(ScriptValue.FromFloat(3.01f)));
  }

  public static void ReportsUnknownSignalAndAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Floodgate());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Floodgate.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Floodgate.Missing", behavior));
  }

  static FloodgateScriptableComponent CreateComponent() {
    var component = new FloodgateScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
