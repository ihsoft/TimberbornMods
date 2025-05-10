// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class HasComponentOperator : BoolOperator {
  public override string Describe() {
    throw new NotImplementedException();
  }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name switch {
        "has" => new HasComponentOperator(context, name, operands),
        _ => null,
    };
  }

  readonly AutomationBehavior _component;
  readonly ScriptingService _scriptingService;

  HasComponentOperator(ExpressionParser.Context context, string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsRange(1, -1);
    _component = context.ScriptHost;
    _scriptingService = context.ScriptingService;

    var testStrings = new List<string>();
    foreach (var operand in operands) {
      if (operand is not ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } componentName) {
        throw new ScriptError.ParsingError("Expected a string literal: " + operand);
      }
      testStrings.Add(componentName.ValueFn().AsString);
    }
    Execute = () => testStrings.All(x => x.Contains(".") ? TryScriptable(x) : TryComponent(x));
  }

  bool TryScriptable(string nameOrPrefix) {
    var actions = _scriptingService.GetActionNamesForBuilding(_component);
    if (actions.Any(x => x.StartsWith(nameOrPrefix) || x == nameOrPrefix)) {
      return true;
    }
    var signals = _scriptingService.GetSignalNamesForBuilding(_component);
    if (signals.Any(x => x.StartsWith(nameOrPrefix) || x == nameOrPrefix)) {
      return true;
    }
    return false;
  }

  bool TryComponent(string componentName) {
    return GetPropertyOperator.GetComponentByName(_component, componentName);
  }
}
