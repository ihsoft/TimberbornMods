// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

interface IValueExpr : IExpression {
   public ScriptValue.TypeEnum ValueType { get; }
   public Func<ScriptValue> ValueFn { get; }

   /// <summary>
   /// Tells if the value is a constant value or an expression that can be reduced to a constant value (e.g. math
   /// operator with constant operands).
   /// </summary>
   public bool IsConstantValue() {
      return IsConstantValueInternal(this);
   }

   static bool IsConstantValueInternal(IExpression expr) {
     return expr switch {
         ConstantValueExpr => true,
         MathOperator mathOperator => mathOperator.Operands.All(IsConstantValueInternal),
         ConcatOperator concatOperator => concatOperator.Operands.All(IsConstantValueInternal),
         _ => false,
     };
   }
}
