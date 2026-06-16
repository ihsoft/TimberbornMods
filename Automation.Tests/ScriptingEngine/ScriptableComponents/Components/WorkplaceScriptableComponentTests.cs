using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.PrioritySystem;
using Timberborn.WorkSystem;

namespace Automation.Tests;

static class WorkplaceScriptableComponentTests {
  public static void ExposesSignalAndActionsForWorkplace() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Workplace { MaxWorkers = 3 });

    Assert.Equal("Workplace.AssignedWorkers", component.GetSignalNamesForBuilding(behavior)[0]);
    Assert.Equal("Workplace.RemoveWorkers", component.GetActionNamesForBuilding(behavior)[0]);
    Assert.Equal("Workplace.SetWorkers", component.GetActionNamesForBuilding(behavior)[1]);

    behavior.SetComponent(new WorkplacePriority());

    Assert.Equal("Workplace.SetPriority", component.GetActionNamesForBuilding(behavior)[2]);
  }

  public static void HidesSignalAndActionsForMissingWorkplace() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    Assert.Equal(0, component.GetSignalNamesForBuilding(behavior).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void ReadsAssignedWorkersSignal() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Workplace { NumberOfAssignedWorkers = 2, MaxWorkers = 3 });

    Assert.Equal(200, component.GetSignalSource("Workplace.AssignedWorkers", behavior)().AsRawNumber);
  }

  public static void BuildsSignalAndActionDefinitions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Workplace { MaxWorkers = 3 });

    var signalDef = component.GetSignalDefinition("Workplace.AssignedWorkers", behavior);
    var actionDef = component.GetActionDefinition("Workplace.SetWorkers", behavior);
    var priorityDef = component.GetActionDefinition("Workplace.SetPriority", behavior);

    Assert.Equal((0, 3), signalDef.Result.DisplayNumericFormatRange);
    Assert.Equal((0, 3), actionDef.Arguments[0].DisplayNumericFormatRange);
    Assert.Equal("VeryLow", priorityDef.Arguments[0].Options[0].Value);
  }

  public static void ExecutesWorkerActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var workplace = new Workplace { DesiredWorkers = 1, MaxWorkers = 3 };
    behavior.SetComponent(workplace);

    component.GetActionExecutor("Workplace.SetWorkers", behavior)([ScriptValue.FromInt(2)]);

    Assert.Equal(2, workplace.DesiredWorkers);
    Assert.Equal(1, workplace.UnassignWorkerIfOverstaffedCalls);

    component.GetActionExecutor("Workplace.RemoveWorkers", behavior)([]);

    Assert.Equal(0, workplace.DesiredWorkers);
  }

  public static void ExecutesPriorityAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    var priority = new WorkplacePriority { Priority = Priority.Normal };
    behavior.SetComponent(new Workplace { MaxWorkers = 3 });
    behavior.SetComponent(priority);

    component.GetActionExecutor("Workplace.SetPriority", behavior)([ScriptValue.FromString("High")]);

    Assert.Equal(Priority.High, priority.Priority);
    Assert.Equal(1, priority.SetPriorityCalls);
  }

  public static void ReportsInvalidWorkersPriorityAndAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Workplace { MaxWorkers = 3 });

    Assert.Throws<ScriptError.ValueOutOfRange>(
        () => component.GetActionExecutor("Workplace.SetWorkers", behavior)([ScriptValue.FromInt(4)]));
    Assert.Throws<ScriptError.BadStateError>(
        () => component.GetActionExecutor("Workplace.SetPriority", behavior)([ScriptValue.FromString("High")]));

    behavior.SetComponent(new WorkplacePriority());

    Assert.Throws<ScriptError.ValueOutOfRange>(
        () => component.GetActionExecutor("Workplace.SetPriority", behavior)([ScriptValue.FromString("Missing")]));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Workplace.Missing", behavior));
  }

  static WorkplaceScriptableComponent CreateComponent() {
    var component = new WorkplaceScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
