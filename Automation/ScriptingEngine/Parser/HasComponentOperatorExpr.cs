// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class HasComponentOperatorExpr : BoolOperatorExpr {
  public override string Describe() {
    throw new NotImplementedException();
  }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name switch {
        "has" => new HasComponentOperatorExpr(context, name, operands),
        _ => null,
    };
  }

  readonly BaseComponent _component;
  readonly ScriptingService _scriptingService;

  HasComponentOperatorExpr(ExpressionParser.Context context, string name, IList<IExpression> operands)
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
    Execute = () => testStrings.All(x => x.EndsWith(".") ? TryScriptable(x) : TryComponent(x));
  }

  bool TryScriptable(string namePrefix) {
    var actions = _scriptingService.GetActionNamesForBuilding(_component);
    if (actions.Any(x => x.StartsWith(namePrefix))) {
      return true;
    }
    var signals = _scriptingService.GetSignalNamesForBuilding(_component);
    if (signals.Any(x => x.StartsWith(namePrefix))) {
      return true;
    }
    return false;
  }

  bool TryComponent(string componentName) {
    return GetPropertyOperatorExpr.GetComponentByName(_component, componentName);
  }
}
