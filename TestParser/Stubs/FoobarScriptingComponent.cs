using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace TestParser.Stubs;

sealed class FoobarScriptingComponent : ScriptableComponentBase {
  public override string Name => "Foobar";

  const string EmptyActionName = "Foobar.EmptyAction";
  const string OneArgumentActionName = "Foobar.OneArgumentAction";

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        EmptyActionName => new ActionDef {
            ScriptName = EmptyActionName,
            DisplayName = "EmptyActionName",
            Arguments = [],
        },
        OneArgumentActionName => new ActionDef {
            ScriptName = OneArgumentActionName,
            DisplayName = "OneArgumentActionName",
            Arguments = [
                new ValueDef {
                    ValueType = ScriptValue.TypeEnum.Number,
                    DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
                },
            ],
        },
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return name switch {
        EmptyActionName => args => {},
        OneArgumentActionName => args => {},
        _ => throw new UnknownActionException(name),
    };
  }
}
