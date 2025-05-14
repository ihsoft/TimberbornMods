// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class HasComponentOperator : BoolOperator {
  public override string Describe() {
    throw new NotImplementedException();
  }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name switch {
        HasSigName => new HasComponentOperator(context, name, operands),
        HasActName => new HasComponentOperator(context, name, operands),
        _ => null,
    };
  }

  const string HasSigName = "?sig";
  const string HasActName = "?act";

  readonly AutomationBehavior _component;
  readonly ScriptingService _scriptingService;

  HasComponentOperator(ExpressionParser.Context context, string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsRange(1, -1);
    _component = context.ScriptHost;
    _scriptingService = context.ScriptingService;

    var testStrings = new List<string>();
    foreach (var operand in operands) {
      if (operand is not SymbolExpr { Value: var testName }) {
        throw new ScriptError.ParsingError("Expected a symbol: " + operand);
      }
      testStrings.Add(testName);
    }
    Execute = name switch {
        HasSigName => () => TrySignals(testStrings),
        HasActName => () => TryActions(testStrings),
        _ => throw new InvalidOperationException("Unknown operator name: " + name),
    };
  }

  bool TrySignals(IList<string> names) {
    try {
      foreach (var name in names) {
        _scriptingService.GetSignalSource(name, _component);
      }
      return true;
    } catch (ScriptError) {
      return false;
    }
  }

  bool TryActions(IList<string> names) {
    try {
      foreach (var name in names) {
        _scriptingService.GetActionExecutor(name, _component);
      }
      return true;
    } catch (ScriptError) {
      return false;
    }
  }
}
