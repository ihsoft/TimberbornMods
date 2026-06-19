using System.Reflection;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BlockSystem;
using Timberborn.WaterBuildings;
using Timberborn.WaterSourceSystem;

namespace Automation.Tests;

static class GameAutomationConflictDetectorTests {
  public static void IgnoresFillValveManualTargetRules() {
    var behavior = CreateBehavior(new FillValve());

    AddRule(behavior, "FillValve.SetHeight(1)");

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void IgnoresThrottlingValveManualFlowRules() {
    var behavior = CreateBehavior(new ThrottlingValve());

    AddRule(behavior, "ThrottlingValve.SetFlow(1)");

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void DetectsFlowControlRulesOnWaterSourceRegulators() {
    var behavior = CreateBehavior(new WaterSourceRegulator());

    AddRule(behavior, "FlowControl.Open()");

    Assert.True(new GameAutomationConflictDetector().HasConflictingRules(behavior));
  }

  public static void IgnoresStateChangesToNonAutomatedProperties() {
    var behavior = CreateBehavior();

    AddRule(behavior, "Pausable.Pause()");
    AddRule(behavior, "Workplace.SetPriority('High')");

    Assert.False(new GameAutomationConflictDetector().HasConflictingRules(behavior));
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

  public static void DetectsStateChangingActionNames() {
    var behavior = CreateBehavior();

    Assert.True(new GameAutomationConflictDetector().IsBuildingStateChangingAction(
        ParseAction(behavior, "FillValve.Open()")));
    Assert.True(new GameAutomationConflictDetector().IsBuildingStateChangingAction(
        ParseAction(behavior, "ThrottlingValve.SetFlow(1)")));
  }

  public static void IgnoresNonStateChangingActionNames() {
    var behavior = CreateBehavior();

    Assert.False(new GameAutomationConflictDetector().IsBuildingStateChangingAction(
        ParseAction(behavior, "Signals.Set('notice', 1)")));
    Assert.False(new GameAutomationConflictDetector().IsBuildingStateChangingAction(
        ParseAction(behavior, "Notifications.SetNoticeIcon('NothingToDo', 'check it')")));
  }

  public static void IgnoresManualWaterValveRuleSaveConflicts() {
    var fillValveBehavior = CreateBehavior(new FillValve());
    var throttlingValveBehavior = CreateBehavior(new ThrottlingValve());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());
    var fillValveRules = new[] {
        Rule(1, ParseAction(fillValveBehavior, "Signals.Set('notice', 1)")),
        Rule(2, ParseAction(fillValveBehavior, "FillValve.Open()")),
    };
    var throttlingValveRules = new[] {
        Rule(1, ParseAction(throttlingValveBehavior, "ThrottlingValve.SetFlow(1)")),
    };

    var fillValveConflicts = detector.GetConflictingRuleNumbers(
        fillValveBehavior, gameAutomationEnabled: true, fillValveRules);
    var throttlingValveConflicts = detector.GetConflictingRuleNumbers(
        throttlingValveBehavior, gameAutomationEnabled: true, throttlingValveRules);

    Assert.Equal(0, fillValveConflicts.Count);
    Assert.Equal(0, throttlingValveConflicts.Count);
  }

  public static void FindsFlowControlRuleSaveConflicts() {
    var behavior = CreateBehavior(new WaterSourceRegulator());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());
    var rules = new[] {
        Rule(1, ParseAction(behavior, "Signals.Set('notice', 1)")),
        Rule(2, ParseAction(behavior, "FlowControl.Open()")),
    };

    var conflicts = detector.GetConflictingRuleNumbers(behavior, gameAutomationEnabled: true, rules);

    Assert.Equal(1, conflicts.Count);
    Assert.Equal(2, conflicts[0]);
  }

  public static void IgnoresRuleSaveConflictsWhenGameAutomationIsDisabled() {
    var behavior = CreateBehavior(new FillValve());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());
    var rules = new[] {
        Rule(1, ParseAction(behavior, "FillValve.Open()")),
    };

    var conflicts = detector.GetConflictingRuleNumbers(behavior, gameAutomationEnabled: false, rules);

    Assert.Equal(0, conflicts.Count);
  }

  public static void IgnoresDisabledAndDeletedRuleSaveConflicts() {
    var behavior = CreateBehavior(new FillValve());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());
    var rules = new[] {
        Rule(1, ParseAction(behavior, "FillValve.Open()"), isEnabled: false),
        Rule(2, ParseAction(behavior, "ThrottlingValve.Close()"), isDeleted: true),
    };

    var conflicts = detector.GetConflictingRuleNumbers(behavior, gameAutomationEnabled: true, rules);

    Assert.Equal(0, conflicts.Count);
  }

  public static void IgnoresNonConflictingRuleCandidates() {
    var behavior = CreateBehavior(new FillValve());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());

    Assert.False(detector.IsConflictingRule(behavior, Rule(1, ParseAction(behavior, "Signals.Set('notice', 1)"))));
    Assert.False(detector.IsConflictingRule(behavior, Rule(2, ParseAction(behavior, "FillValve.Open()"))));
  }

  public static void IgnoresDisabledAndDeletedRuleCandidates() {
    var behavior = CreateBehavior(new FillValve());
    var detector = new GameAutomationRuleSaveConflictDetector(new GameAutomationConflictDetector());

    Assert.False(detector.IsConflictingRule(
        behavior, Rule(1, ParseAction(behavior, "FillValve.Open()"), isEnabled: false)));
    Assert.False(detector.IsConflictingRule(
        behavior, Rule(2, ParseAction(behavior, "FillValve.Close()"), isDeleted: true)));
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

  static GameAutomationRuleSaveConflictDetector.RuleCandidate Rule(
      int ruleNumber, ActionOperator parsedAction, bool isDeleted = false, bool isEnabled = true) {
    return new GameAutomationRuleSaveConflictDetector.RuleCandidate(ruleNumber, isDeleted, isEnabled, parsedAction);
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

    var flowControl = new TestScriptable("FlowControl");
    flowControl.RegisterAction("FlowControl.Open");
    flowControl.RegisterAction("FlowControl.Close");

    TestScripting.CreateService(fillValve, throttlingValve, pausable, workplace, signals, notifications, flowControl);
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
