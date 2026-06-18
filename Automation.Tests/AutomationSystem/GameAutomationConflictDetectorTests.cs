using System.Reflection;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BlockSystem;
using Timberborn.WaterBuildings;

namespace Automation.Tests;

static class GameAutomationConflictDetectorTests {
  public static void DetectsFillValveStateChangingRules() {
    var behavior = CreateBehavior(new FillValve());

    AddRule(behavior, "FillValve.SetHeight(1)");

    Assert.True(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void DetectsThrottlingValveStateChangingRules() {
    var behavior = CreateBehavior(new ThrottlingValve());

    AddRule(behavior, "ThrottlingValve.SetFlow(1)");

    Assert.True(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void DetectsStateChangingRulesForOtherBuildings() {
    var behavior = CreateBehavior();

    AddRule(behavior, "Pausable.Pause()");
    AddRule(behavior, "Workplace.SetPriority('High')");

    Assert.True(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void IgnoresSignalRules() {
    var behavior = CreateBehavior(new FillValve());

    AddRule(behavior, "Signals.Set('notice', 1)");

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void IgnoresNotificationRules() {
    var behavior = CreateBehavior();

    AddRule(behavior, "Notifications.SetNoticeIcon('NothingToDo', 'check it')");

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void IgnoresDisabledRules() {
    var behavior = CreateBehavior(new ThrottlingValve());

    AddRule(behavior, "ThrottlingValve.Close()", enabled: false);

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  static AutomationBehavior CreateBehavior(params object[] components) {
    var behavior = new AutomationBehavior {
        Name = "TestBehavior",
    };
    behavior.SetComponent(new BlockObject { IsFinished = true });
    foreach (var component in components) {
      SetComponent(behavior, component);
    }
    behavior.InjectDependencies(new AutomationService());
    behavior.Awake();
    return behavior;
  }

  static void SetComponent(AutomationBehavior behavior, object component) {
    var method = typeof(Timberborn.BaseComponentSystem.BaseComponent)
        .GetMethod(nameof(Timberborn.BaseComponentSystem.BaseComponent.SetComponent))
        .MakeGenericMethod(component.GetType());
    method.Invoke(behavior, [component]);
  }

  static void AddRule(AutomationBehavior behavior, string expression, bool enabled = true) {
    var actionOperator = ParseAction(behavior, expression);
    var condition = new ScriptedCondition {
        Enabled = enabled,
    };
    var action = new ScriptedAction {
        ParsingResult = new ParsingResult {
            ParsedExpression = actionOperator,
        },
    };
    action.SetExpression(expression);
    behavior.AddRule(condition, action);
  }

  static ActionOperator ParseAction(AutomationBehavior behavior, string expression) {
    RegisterScriptables();
    var parserFactory = CreateParserFactory();
    var action = parserFactory.ParseAction(expression, behavior, out var result, parserFactory.PythonSyntaxParser);
    if (result.LastScriptError != null) {
      throw result.LastScriptError;
    }
    return action;
  }

  static void RegisterScriptables() {
    var fillValve = new TestScriptable("FillValve");
    fillValve.RegisterAction("FillValve.Open");
    fillValve.RegisterAction("FillValve.Close");
    fillValve.RegisterAction("FillValve.SetHeight", ScriptValue.TypeEnum.Number);

    var throttlingValve = new TestScriptable("ThrottlingValve");
    throttlingValve.RegisterAction("ThrottlingValve.Open");
    throttlingValve.RegisterAction("ThrottlingValve.Close");
    throttlingValve.RegisterAction("ThrottlingValve.SetFlow", ScriptValue.TypeEnum.Number);

    var pausable = new TestScriptable("Pausable");
    pausable.RegisterAction("Pausable.Pause");
    pausable.RegisterAction("Pausable.Unpause");

    var workplace = new TestScriptable("Workplace");
    workplace.RegisterAction("Workplace.RemoveWorkers");
    workplace.RegisterAction("Workplace.SetWorkers", ScriptValue.TypeEnum.Number);
    workplace.RegisterAction("Workplace.SetPriority", ScriptValue.TypeEnum.String);

    var signals = new TestScriptable("Signals");
    signals.RegisterAction("Signals.Set", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.Number);

    var notifications = new TestScriptable("Notifications");
    notifications.RegisterAction("Notifications.SetNoticeIcon", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.String);

    TestScripting.CreateService(fillValve, throttlingValve, pausable, workplace, signals, notifications);
  }

  static ParserFactory CreateParserFactory() {
    var constructor = typeof(ParserFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(LispSyntaxParser), typeof(PythonSyntaxParser)],
        null);
    return (ParserFactory)constructor.Invoke([new LispSyntaxParser(), new PythonSyntaxParser()]);
  }
}
