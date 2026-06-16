using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace Automation.Tests;

static class ScriptingServiceTests {
  public static void RegistersAndLooksUpScriptables() {
    var behavior = new AutomationBehavior();
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal("Signals.Var1", ScriptValue.TypeEnum.Number, () => ScriptValue.FromInt(7));
    signals.RegisterAction("Signals.Set", ScriptValue.TypeEnum.String, ScriptValue.TypeEnum.Number);
    var service = TestScripting.CreateService(signals);

    Assert.Equal("Signals", service.GetScriptableNames()[0]);
    Assert.Equal("Signals.Var1", service.GetSignalNamesForBuilding(behavior)[0]);
    Assert.Equal("Signals.Set", service.GetActionNamesForBuilding(behavior)[0]);
    Assert.Equal(700, service.GetSignalSource("Signals.Var1", behavior)().AsRawNumber);
    Assert.Equal("Signals.Var1", service.GetSignalDefinition("Signals.Var1", behavior).ScriptName);
    Assert.Equal("Signals.Set", service.GetActionDefinition("Signals.Set", behavior).ScriptName);
    Assert.True(service.GetActionExecutor("Signals.Set", behavior) != null);
  }

  public static void UnknownScriptableReportsParsingError() {
    var behavior = new AutomationBehavior();
    var service = TestScripting.CreateService();

    Assert.Throws<ScriptError.ParsingError>(() => service.GetSignalSource("Missing.Signal", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => service.GetActionDefinition("Missing.Action", behavior));
  }

  public static void RegistersAndUnregistersSignalsFromExpression() {
    var behavior = new AutomationBehavior();
    var signals = new TestScriptable("Signals");
    signals.RegisterSignal("Signals.Var1", ScriptValue.TypeEnum.Number);
    signals.RegisterSignal("Signals.Var2", ScriptValue.TypeEnum.Number);
    var service = TestScripting.CreateService(signals);
    var listener = new TestSignalListener(behavior);
    var expression = ParsePython("Signals.Var1 == 0 and Signals.Var2 == 0");

    var registeredSignals = service.RegisterSignals(expression, listener);
    service.UnregisterSignals(registeredSignals, listener);

    Assert.Equal(2, registeredSignals.Count);
    Assert.Equal("Signals.Var1", registeredSignals[0].SignalName);
    Assert.Equal("Signals.Var2", registeredSignals[1].SignalName);
    Assert.Equal(2, signals.RegisteredCallbacks.Count);
    Assert.Equal(2, signals.UnregisteredCallbacks.Count);
    Assert.Same(listener, signals.RegisteredCallbacks[0].Host);
    Assert.Same(listener, signals.UnregisteredCallbacks[1].Host);
  }

  public static void InstallsAndUninstallsActionsFromExpression() {
    var behavior = new AutomationBehavior();
    var debug = new TestScriptable("Debug");
    debug.RegisterAction("Debug.Log", ScriptValue.TypeEnum.String);
    var service = TestScripting.CreateService(debug);
    var expression = ParsePython("Debug.Log('hello')");

    var installedActions = service.InstallActions(expression, behavior);
    service.UninstallActions(installedActions, behavior);

    Assert.Equal(1, installedActions.Count);
    Assert.Equal("Debug.Log", installedActions[0].ActionName);
    Assert.Equal(1, debug.InstalledActions.Count);
    Assert.Equal(1, debug.UninstalledActions.Count);
    Assert.Same(behavior, debug.InstalledActions[0].Behavior);
    Assert.Same(behavior, debug.UninstalledActions[0].Behavior);
  }

  public static void MaintainsExecutionStack() {
    var service = TestScripting.CreateService();
    var first = "first";
    var second = "second";

    service.PushToExecutionStack(first);
    service.PushToExecutionStack(second);

    Assert.Equal(2, service.GetExecutionStackSize());
    Assert.Equal("second", service.CaptureCallStack()[0]);
    Assert.Equal("first", service.CaptureCallStack()[1]);
    Assert.Throws<InvalidOperationException>(() => service.PopFromExecutionStack(first));
    service.PopFromExecutionStack(second);
    service.PopFromExecutionStack(first);
    Assert.Equal(0, service.GetExecutionStackSize());
    Assert.Throws<InvalidOperationException>(() => service.PopFromExecutionStack(first));
  }

  public static void NotifySignalListenerWrapsCallbackWithExecutionStack() {
    var service = TestScripting.CreateService();
    var listener = new TestSignalListener(new AutomationBehavior()) {
        OnValueChangedAction = signalName => {
          Assert.Equal("Signals.Var1", signalName);
          Assert.Equal(1, service.GetExecutionStackSize());
          Assert.True(service.CaptureCallStack()[0].Contains("SignalName=Signals.Var1"));
        },
    };

    service.NotifySignalListener("Signals.Var1", listener);

    Assert.Equal(1, listener.Calls);
    Assert.Equal(0, service.GetExecutionStackSize());
  }

  static IExpression ParsePython(string expression) {
    var result = new PythonSyntaxParser().Parse(expression, new AutomationBehavior());
    if (result.LastScriptError != null) {
      throw new InvalidOperationException(result.LastError);
    }
    return result.ParsedExpression;
  }

  sealed class TestSignalListener(AutomationBehavior behavior)
      : IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components.ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }
    public Action<string> OnValueChangedAction { get; init; }

    public void OnValueChanged(string signalName) {
      Calls++;
      OnValueChangedAction?.Invoke(signalName);
    }
  }
}
