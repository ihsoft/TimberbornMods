using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Automation;
using Timberborn.Localization;

namespace Automation.Tests;

static class AutomatorScriptableComponentTests {
  public static void ExposesSignalForTransmitterAutomator() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    Assert.Equal("Automator.State", component.GetSignalNamesForBuilding(behavior)[0]);
  }

  public static void HidesSignalForMissingOrNonTransmitterAutomator() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = false });

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ReadsAutomatorState() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var automator = new Automator { IsTransmitter = true, State = AutomatorState.On };
    behavior.SetComponent(automator);

    Assert.Equal(100, component.GetSignalSource("Automator.State", behavior)().AsRawNumber);

    automator.State = AutomatorState.Off;

    Assert.Equal(0, component.GetSignalSource("Automator.State", behavior)().AsRawNumber);
  }

  public static void BuildsStateSignalDefinition() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    var signalDef = component.GetSignalDefinition("Automator.State", behavior);

    Assert.Equal("Automator.State", signalDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, signalDef.Result.DisplayNumericFormat);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Automator { IsTransmitter = true });

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Automator.Missing", behavior));
  }

  static AutomatorScriptableComponent CreateComponent() {
    var component = new AutomatorScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
