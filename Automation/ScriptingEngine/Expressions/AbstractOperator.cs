// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.Settings;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

abstract class AbstractOperator(IList<IExpression> operands) : IExpression {

  public readonly IList<IExpression> Operands = operands;

  /// <inheritdoc/>
  public void VisitNodes(Action<IExpression> visitorFn) {
    visitorFn(this);
    foreach (var expression in Operands) {
      expression.VisitNodes(visitorFn);
    }
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}";
  }

  /// <summary>Unwraps operands to the binary tree if there are more than 2 operands in the expression.</summary>
  /// <remarks>Operands that allow multi-arguments must override this method.</remarks>
  /// <exception cref="InvalidOperationException">if more than 2 operands and no override given.</exception>
  public virtual IList<IExpression> GetReducedOperands() {
    return Operands.Count <= 2
        ? Operands
        : throw new InvalidOperationException($"Operands reducing is not supported");
  }

  protected void AssertNumberOfOperandsExact(int expected) {
    var count = Operands.Count;
    if (expected != count) {
      throw new ScriptError.ParsingError($"Operator '{this}' requires {expected} arguments, but got {count}");
    }
  }

  protected void AssertNumberOfOperandsRange(int min, int max) {
    var count = Operands.Count;
    if (min >= 0 && count < min) {
      throw new ScriptError.ParsingError($"Operator '{this}' requires at least {min} arguments, but got {count}");
    }
    if (max >= 0 && count > max) {
      throw new ScriptError.ParsingError($"Operator '{this}' requires at most {max} arguments, but got {count}");
    }
  }

  /// <summary>Returns a binary tree of expressions if there are more than 2 operands in the operator.</summary>
  /// <remarks>Use it to reduce multi-argument operators to binary operators.</remarks>
  /// <param name="reduceOperandsFn">
  /// Function to join left and right operands. It must return the same type as the operator being reduced.
  /// </param>
  /// <exception cref="InvalidOperationException">if reduce function returns incompatible type.</exception>
  /// <seealso cref="GetReducedOperands"/>
  protected IList<IExpression> ReducedOperands(Func<IList<IExpression>, AbstractOperator> reduceOperandsFn) {
    if (Operands.Count < 3) {
      return Operands;
    }
    var operands = new List<IExpression>(Operands);  // MUST obtain a copy! We will be modifying.
    while (operands.Count > 2) {
      var reducedOperand = reduceOperandsFn([operands[0], operands[1]]);
      if (reducedOperand.GetType() != GetType()) {
        throw new InvalidOperationException(
            $"Reduce operands function must return type {GetType()}, but got {reducedOperand.GetType()}");
      }
      operands.RemoveAt(0);
      operands.RemoveAt(0);
      operands.Insert(0, reducedOperand);
    }
    return operands;
  }
}
