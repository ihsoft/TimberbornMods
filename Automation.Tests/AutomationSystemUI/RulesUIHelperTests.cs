using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.AutomationSystemUI;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using Timberborn.Localization;

namespace Automation.Tests;

static class RulesUIHelperTests {
  public static void ListsOnlyBuildingNumberSignalsForExport() {
    var scriptable = new TestScriptable("Test");
    scriptable.RegisterSignal("Test.BuildingNumber", ScriptValue.TypeEnum.Number);
    scriptable.RegisterSignal("Test.GlobalNumber", ScriptValue.TypeEnum.Number, scope: SignalDef.ScopeEnum.Global);
    scriptable.RegisterSignal("Test.BuildingString", ScriptValue.TypeEnum.String);
    var helper = CreateHelper(TestScripting.CreateService(scriptable));

    helper.SetBuilding(new AutomationBehavior());

    Assert.Equal(1, helper.BuildingSignalNames.Count);
    Assert.Equal("Test.BuildingNumber", helper.BuildingSignalNames[0]);
    Assert.Equal(1, helper.BuildingSignals.Count);
    Assert.Equal("Test.BuildingNumber", helper.BuildingSignals[0].SignalName);
  }

  static RulesUIHelper CreateHelper(ScriptingService scriptingService) {
    var constructor = typeof(RulesUIHelper).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(ScriptingService), typeof(ILoc)],
        null);
    return (RulesUIHelper)constructor.Invoke([scriptingService, new TestLoc()]);
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
