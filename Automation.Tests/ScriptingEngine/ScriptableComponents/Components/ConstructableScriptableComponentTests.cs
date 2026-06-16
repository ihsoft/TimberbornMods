using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.Localization;

namespace Automation.Tests;

static class ConstructableScriptableComponentTests {
  public static void ExposesSignalsForUnfinishedBuilding() {
    var component = CreateComponent();
    var behavior = CreateBehavior(isFinished: false);

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("Constructable.OnUnfinished.State", signalNames[0]);
    Assert.Equal("Constructable.OnUnfinished.Progress", signalNames[1]);
  }

  public static void HidesSignalsForFinishedBuilding() {
    var component = CreateComponent();
    var behavior = CreateBehavior(isFinished: true);

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
  }

  public static void ReadsStateAndProgressSignals() {
    var component = CreateComponent();
    var behavior = CreateBehavior(isFinished: false);
    behavior.SetComponent(new ConstructionSite { BuildTimeProgress = 0.25f });

    Assert.Equal("", component.GetSignalSource("Constructable.OnUnfinished.State", behavior)().AsString);
    Assert.Equal(25, component.GetSignalSource("Constructable.OnUnfinished.Progress", behavior)().AsRawNumber);

    behavior.BlockObject.IsFinished = true;

    Assert.Equal("finished", component.GetSignalSource("Constructable.OnUnfinished.State", behavior)().AsString);
  }

  public static void BuildsSignalDefinitions() {
    var component = CreateComponent();
    var behavior = CreateBehavior(isFinished: false);

    var stateDef = component.GetSignalDefinition("Constructable.OnUnfinished.State", behavior);
    var progressDef = component.GetSignalDefinition("Constructable.OnUnfinished.Progress", behavior);

    Assert.Equal(ScriptValue.TypeEnum.String, stateDef.Result.ValueType);
    Assert.Equal("finished", stateDef.Result.Options[0].Value);
    Assert.Equal(ScriptValue.TypeEnum.Number, progressDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Percent, progressDef.Result.DisplayNumericFormat);
    Assert.Equal((0, 100), progressDef.Result.DisplayNumericFormatRange);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = CreateBehavior(isFinished: false);

    Assert.Throws<ScriptError.ParsingError>(
        () => component.GetSignalDefinition("Constructable.OnUnfinished.Missing", behavior));
  }

  static AutomationBehavior CreateBehavior(bool isFinished) {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new BlockObject { IsFinished = isFinished });
    behavior.Awake();
    return behavior;
  }

  static ConstructableScriptableComponent CreateComponent() {
    var component = new ConstructableScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
