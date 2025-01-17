// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;

namespace IgorZ.Automation.ScriptingEngine.Parser;

abstract class IExpression {
  //FIXME: keep teh string oroginal (or range reference) for better debugging.
  //FIXME: or restore the sting frowm what was parsed (a sample, niot full string)
  protected static T GetOperand<T>(IExpression expression) where T : IExpression {
    if (expression is not T result) {
      throw new ScriptError($"Expected {typeof(T).Name}, but found {expression.GetType().Name}: {expression}");
    }
    return result;
  }

  protected static void AsserNumberOfOperands(IExpression expression, int expected, int found) {
    if (expected != found) {
      throw new ScriptError(expression.GetType().Name + ": expected " + expected + " operands, but found " + found);
    }
  }

  //FIXME: move to abstract operatorexpr
  protected static void AsserNumberOfOperands(string operatorName, IList<IExpression> arguments, int expected) {
    var count = arguments.Count;
    if (expected < 0) {
      if (arguments.Count < -expected) {
        throw new ScriptError($"Operator '{operatorName}' requires at least {expected} arguments, got {count}");
      }
    } else {
      if (expected != arguments.Count) {
        throw new ScriptError($"Operator '{operatorName}' requires {expected} arguments, got {count}");
      }
    }
  }
}
