using System;
using System.Collections.Generic;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace Automation.Tests;

static class TestScripting {
  public static ScriptingService CreateService(params TestScriptable[] scriptables) {
    var constructor = typeof(ScriptingService).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [],
        null);
    var service = (ScriptingService)constructor.Invoke([]);
    foreach (var scriptable in scriptables) {
      service.RegisterScriptable(scriptable);
    }
    return service;
  }
}

sealed class TestScriptable : IScriptable {
  readonly Dictionary<string, (SignalDef Def, Func<ScriptValue> Source)> _signals = new();
  readonly Dictionary<string, (ActionDef Def, Action<ScriptValue[]> Executor)> _actions = new();

  public string Name { get; }
  public readonly List<(SignalOperator Signal, ISignalListener Host)> RegisteredCallbacks = [];
  public readonly List<(SignalOperator Signal, ISignalListener Host)> UnregisteredCallbacks = [];
  public readonly List<(ActionOperator Action, AutomationBehavior Behavior)> InstalledActions = [];
  public readonly List<(ActionOperator Action, AutomationBehavior Behavior)> UninstalledActions = [];

  public TestScriptable(string name) {
    Name = name;
  }

  public void RegisterSignal(string name, ScriptValue.TypeEnum valueType, Func<ScriptValue> source = null) {
    _signals[name] = (
        new SignalDef {
            ScriptName = name,
            DisplayName = name,
            Result = new ValueDef {
                ValueType = valueType,
                DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
            },
        },
        source ?? (() => valueType == ScriptValue.TypeEnum.String
            ? ScriptValue.FromString(name)
            : ScriptValue.FromInt(0))
    );
  }

  public void RegisterAction(string name, params ScriptValue.TypeEnum[] argumentTypes) {
    var arguments = new ValueDef[argumentTypes.Length];
    for (var i = 0; i < argumentTypes.Length; i++) {
      arguments[i] = new ValueDef {
          ValueType = argumentTypes[i],
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
      };
    }
    _actions[name] = (
        new ActionDef {
            ScriptName = name,
            DisplayName = name,
            Arguments = arguments,
        },
        _ => {}
    );
  }

  public string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return [.._signals.Keys];
  }

  public Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    return _signals.TryGetValue(name, out var signal)
        ? signal.Source
        : throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  public SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return _signals.TryGetValue(name, out var signal)
        ? signal.Def
        : throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  public string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return [.._actions.Keys];
  }

  public Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return _actions.TryGetValue(name, out var action)
        ? action.Executor
        : throw new ScriptError.ParsingError("Unknown action: " + name);
  }

  public ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return _actions.TryGetValue(name, out var action)
        ? action.Def
        : throw new ScriptError.ParsingError("Unknown action: " + name);
  }

  public void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    RegisteredCallbacks.Add((signalOperator, host));
  }

  public void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    UnregisteredCallbacks.Add((signalOperator, host));
  }

  public void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    InstalledActions.Add((actionOperator, behavior));
  }

  public void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    UninstalledActions.Add((actionOperator, behavior));
  }
}
