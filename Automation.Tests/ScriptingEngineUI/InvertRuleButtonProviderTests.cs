using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngineUI;
using Timberborn.AutomationBuildings;
using Timberborn.PowerManagement;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.WaterBuildings;
using Timberborn.WorkSystem;

namespace Automation.Tests;

static class InvertRuleButtonProviderTests {
  public static void InvertsFillValveSetHeightArgumentWithoutChangingAction() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve { MinTargetHeight = 2, MaxTargetHeight = 5 });
    var parserFactory = CreateParserFactory();
    RegisterFillValveScriptableComponent();

    var zeroAction = ParseAction(parserFactory, behavior, "FillValve.SetHeight(0)");
    var maxAction = ParseAction(parserFactory, behavior, "FillValve.SetHeight(3)");

    Assert.Equal("(act FillValve.SetHeight 300)", MakeInvertedActionExpression(parserFactory, zeroAction, behavior));
    Assert.Equal("(act FillValve.SetHeight 0)", MakeInvertedActionExpression(parserFactory, maxAction, behavior));
  }

  public static void InvertsFillValveOpenAndCloseActions() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new FillValve { MinTargetHeight = 2, MaxTargetHeight = 5 });
    var parserFactory = CreateParserFactory();
    RegisterFillValveScriptableComponent();

    var openAction = ParseAction(parserFactory, behavior, "FillValve.Open()");
    var closeAction = ParseAction(parserFactory, behavior, "FillValve.Close()");

    Assert.Equal("(act FillValve.Close)", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act FillValve.Open)", MakeInvertedActionExpression(parserFactory, closeAction, behavior));
  }

  public static void InvertsGenericOpenAndCloseActions() {
    var behavior = new AutomationBehavior();
    var parserFactory = CreateParserFactory();
    RegisterGenericOpenCloseScriptable();

    var openAction = ParseAction(parserFactory, behavior, "TestDoor.Open()");
    var closeAction = ParseAction(parserFactory, behavior, "TestDoor.Close()");

    Assert.Equal("(act TestDoor.Close)", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act TestDoor.Open)", MakeInvertedActionExpression(parserFactory, closeAction, behavior));
  }

  public static void InvertsGenericSetStateOpenAndClosedValues() {
    var behavior = new AutomationBehavior();
    var parserFactory = CreateParserFactory();
    RegisterGenericSetStateScriptable();

    var openAction = ParseAction(parserFactory, behavior, "TestGate.SetState('open')");
    var closedAction = ParseAction(parserFactory, behavior, "TestGate.SetState('closed')");
    var automatedAction = ParseAction(parserFactory, behavior, "TestGate.SetState('automated')");

    Assert.Equal("(act TestGate.SetState 'closed')", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act TestGate.SetState 'open')", MakeInvertedActionExpression(parserFactory, closedAction, behavior));
    Assert.Equal(null, MakeInvertedActionExpression(parserFactory, automatedAction, behavior));
  }

  public static void InvertsThrottlingValveSetFlowArgumentWithoutChangingAction() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new ThrottlingValve { MaxOutflowLimit = 2 });
    var parserFactory = CreateParserFactory();
    RegisterThrottlingValveScriptableComponent();

    var zeroAction = ParseAction(parserFactory, behavior, "ThrottlingValve.SetFlow(0)");
    var maxAction = ParseAction(parserFactory, behavior, "ThrottlingValve.SetFlow(2)");

    Assert.Equal(
        "(act ThrottlingValve.SetFlow 200)",
        MakeInvertedActionExpression(parserFactory, zeroAction, behavior));
    Assert.Equal("(act ThrottlingValve.SetFlow 0)", MakeInvertedActionExpression(parserFactory, maxAction, behavior));
  }

  public static void InvertsThrottlingValveOpenAndCloseActions() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new ThrottlingValve { MaxOutflowLimit = 2 });
    var parserFactory = CreateParserFactory();
    RegisterThrottlingValveScriptableComponent();

    var openAction = ParseAction(parserFactory, behavior, "ThrottlingValve.Open()");
    var closeAction = ParseAction(parserFactory, behavior, "ThrottlingValve.Close()");

    Assert.Equal("(act ThrottlingValve.Close)", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act ThrottlingValve.Open)", MakeInvertedActionExpression(parserFactory, closeAction, behavior));
  }

  public static void InvertsClutchEngageAndDisengageActions() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Clutch());
    var parserFactory = CreateParserFactory();
    RegisterClutchScriptableComponent();

    var engageAction = ParseAction(parserFactory, behavior, "Clutch.Engage()");
    var disengageAction = ParseAction(parserFactory, behavior, "Clutch.Disengage()");

    Assert.Equal("(act Clutch.Disengage)", MakeInvertedActionExpression(parserFactory, engageAction, behavior));
    Assert.Equal("(act Clutch.Engage)", MakeInvertedActionExpression(parserFactory, disengageAction, behavior));
  }

  public static void InvertsGenericSetStateOnAndOffValues() {
    var behavior = new AutomationBehavior();
    var parserFactory = CreateParserFactory();
    RegisterGenericSwitchStateScriptable();

    var onAction = ParseAction(parserFactory, behavior, "TestSwitch.SetState('on')");
    var offAction = ParseAction(parserFactory, behavior, "TestSwitch.SetState('off')");

    Assert.Equal("(act TestSwitch.SetState 'off')", MakeInvertedActionExpression(parserFactory, onAction, behavior));
    Assert.Equal("(act TestSwitch.SetState 'on')", MakeInvertedActionExpression(parserFactory, offAction, behavior));
  }

  public static void InvertsGateOpenAndClosedStates() {
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Gate());
    var parserFactory = CreateParserFactory();
    RegisterGateScriptableComponent();

    var openAction = ParseAction(parserFactory, behavior, "Gate.SetState('open')");
    var closedAction = ParseAction(parserFactory, behavior, "Gate.SetState('closed')");
    var automatedAction = ParseAction(parserFactory, behavior, "Gate.SetState('automated')");

    Assert.Equal("(act Gate.SetState 'closed')", MakeInvertedActionExpression(parserFactory, openAction, behavior));
    Assert.Equal("(act Gate.SetState 'open')", MakeInvertedActionExpression(parserFactory, closedAction, behavior));
    Assert.Equal(null, MakeInvertedActionExpression(parserFactory, automatedAction, behavior));
  }

  public static void InvertsTimeWorkingHoursConditionBySwitchingStringValue() {
    var behavior = new AutomationBehavior();
    var parserFactory = CreateParserFactory();
    RegisterTimeScriptableComponent();

    var workingCondition = ParseCondition(parserFactory, behavior, "Time.WorkingHours == 'Working'");
    var offHoursCondition = ParseCondition(parserFactory, behavior, "Time.WorkingHours != 'OffHours'");

    Assert.Equal(
        "(eq (sig Time.WorkingHours) 'OffHours')",
        MakeInvertedConditionExpression(parserFactory, workingCondition));
    Assert.Equal(
        "(ne (sig Time.WorkingHours) 'Working')",
        MakeInvertedConditionExpression(parserFactory, offHoursCondition));
  }

  public static void DoesNotSpecialInvertOtherTimeConditions() {
    var behavior = new AutomationBehavior();
    var parserFactory = CreateParserFactory();
    RegisterTimeScriptableComponent();

    var condition = ParseCondition(parserFactory, behavior, "Time.Day == 1");

    Assert.Equal(null, MakeInvertedConditionExpression(parserFactory, condition));
  }

  static string MakeInvertedConditionExpression(ParserFactory parserFactory, BooleanOperator condition) {
    var provider = new InvertRuleButtonProvider(parserFactory);
    var method = typeof(InvertRuleButtonProvider).GetMethod(
        "MakeInvertedConditionExpression",
        BindingFlags.Instance | BindingFlags.NonPublic);
    return (string)method.Invoke(provider, [condition]);
  }

  static string MakeInvertedActionExpression(
      ParserFactory parserFactory, ActionOperator action, AutomationBehavior behavior) {
    var provider = new InvertRuleButtonProvider(parserFactory);
    var method = typeof(InvertRuleButtonProvider).GetMethod(
        "MakeInvertedActionExpression",
        BindingFlags.Instance | BindingFlags.NonPublic);
    return (string)method.Invoke(provider, [action, behavior]);
  }

  static BooleanOperator ParseCondition(ParserFactory parserFactory, AutomationBehavior behavior, string expression) {
    var condition = parserFactory.ParseCondition(expression, behavior, out var result, parserFactory.PythonSyntaxParser);
    if (result.LastScriptError != null) {
      throw result.LastScriptError;
    }
    return condition;
  }

  static ActionOperator ParseAction(ParserFactory parserFactory, AutomationBehavior behavior, string expression) {
    var action = parserFactory.ParseAction(expression, behavior, out var result, parserFactory.PythonSyntaxParser);
    if (result.LastScriptError != null) {
      throw result.LastScriptError;
    }
    return action;
  }

  static ParserFactory CreateParserFactory() {
    var constructor = typeof(ParserFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(LispSyntaxParser), typeof(PythonSyntaxParser)],
        null);
    return (ParserFactory)constructor.Invoke([new LispSyntaxParser(), new PythonSyntaxParser()]);
  }

  static void RegisterFillValveScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = new FillValveScriptableComponent();
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  static void RegisterThrottlingValveScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = new ThrottlingValveScriptableComponent();
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  static void RegisterTimeScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = CreateTimeScriptableComponent(service);
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  static TimeScriptableComponent CreateTimeScriptableComponent(ScriptingService service) {
    var constructor = typeof(TimeScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [
            typeof(EventBus),
            typeof(IDayNightCycle),
            typeof(ITimeTriggerFactory),
            typeof(WorkingHoursManager),
            typeof(ReferenceManager),
        ],
        null);
    return (TimeScriptableComponent)constructor.Invoke(
        [new EventBus(), new TestDayNightCycle(), new TestTimeTriggerFactory(), new WorkingHoursManager(),
            new ReferenceManager(service)]);
  }

  static void RegisterClutchScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = new ClutchScriptableComponent();
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  static void RegisterGateScriptableComponent() {
    var service = TestScripting.CreateService();
    var component = new GateScriptableComponent();
    component.InjectDependencies(new TestLoc(), service);
    component.Load();
  }

  static void RegisterGenericOpenCloseScriptable() {
    var scriptable = new TestScriptable("TestDoor");
    scriptable.RegisterAction("TestDoor.Open");
    scriptable.RegisterAction("TestDoor.Close");
    TestScripting.CreateService(scriptable);
  }

  static void RegisterGenericSetStateScriptable() {
    var scriptable = new TestScriptable("TestGate");
    scriptable.RegisterStringAction("TestGate.SetState", "open", "closed", "automated");
    TestScripting.CreateService(scriptable);
  }

  static void RegisterGenericSwitchStateScriptable() {
    var scriptable = new TestScriptable("TestSwitch");
    scriptable.RegisterStringAction("TestSwitch.SetState", "on", "off");
    TestScripting.CreateService(scriptable);
  }

  sealed class TestDayNightCycle : IDayNightCycle {
    public int DayNumber => 1;
    public float HoursPassedToday => 0;
  }

  sealed class TestTimeTriggerFactory : ITimeTriggerFactory {
    public ITimeTrigger Create(System.Action action, float delayInDays) {
      return new TestTimeTrigger();
    }
  }

  sealed class TestTimeTrigger : ITimeTrigger {
    public bool InProgress { get; private set; }

    public void Resume() {
      InProgress = true;
    }

    public void Pause() {
      InProgress = false;
    }
  }

  sealed class TestLoc : Timberborn.Localization.ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
