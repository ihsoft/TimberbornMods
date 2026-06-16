using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;

namespace IgorZ.Automation.ScriptingEngine.Core {
  sealed class ScriptingService {
    public static ScriptingService Instance { get; } = new();

    readonly Dictionary<string, (SignalDef Def, Func<ScriptValue> Source)> _signals = new();
    readonly Dictionary<string, (ActionDef Def, Action<ScriptValue[]> Executor)> _actions = new();

    public void Reset() {
      _signals.Clear();
      _actions.Clear();
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

    public SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
      return _signals.TryGetValue(name, out var signal)
          ? signal.Def
          : throw new ScriptError.ParsingError("Unknown signal: " + name);
    }

    public Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
      return _signals.TryGetValue(name, out var signal)
          ? signal.Source
          : throw new ScriptError.ParsingError("Unknown signal: " + name);
    }

    public ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
      return _actions.TryGetValue(name, out var action)
          ? action.Def
          : throw new ScriptError.ParsingError("Unknown action: " + name);
    }

    public Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
      return _actions.TryGetValue(name, out var action)
          ? action.Executor
          : throw new ScriptError.ParsingError("Unknown action: " + name);
    }
  }
}

namespace IgorZ.Automation.Settings {
  static class ScriptEngineSettings {
    public static bool CheckArgumentValues => true;
  }

  static class ScriptEditorSettings {
    public enum ScriptSyntax {
      Lisp,
      Python,
    }

    public static ScriptSyntax DefaultScriptSyntax { get; set; } = ScriptSyntax.Python;
  }
}

namespace IgorZ.TimberDev.UI {
  public sealed record DropdownItem(string Value, string Text);
}

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components {
  static class InventoryScriptableComponent {
    public static Timberborn.BaseComponentSystem.BaseComponent GetInventory(
        Timberborn.BaseComponentSystem.BaseComponent building, bool throwIfNotFound = true) {
      return null;
    }
  }
}
