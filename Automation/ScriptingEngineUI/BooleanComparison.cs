// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;

namespace IgorZ.Automation.ScriptingEngineUI;

static class BooleanComparison {
  /// <summary>Returns whether a numeric value is represented as a Boolean in the constructor UI.</summary>
  public static bool IsBoolean(ValueDef valueDef) {
    return valueDef.DisplayNumericFormat == ValueDef.NumericFormatEnum.Boolean;
  }

  /// <summary>Tries to interpret a comparison as the UI Boolean state it represents.</summary>
  /// <remarks>
  /// The canonical forms are equality with zero for Off and inequality with zero for On. Equality with the recommended
  /// true value, 1.0, is accepted so existing Automator.State rules remain editable.
  /// </remarks>
  public static bool TryGetState(ComparisonOperator comparison, out bool state) {
    state = false;
    if (comparison.Left is not SignalOperator signal
        || !IsBoolean(signal.SignalDef.Result)
        || comparison.Right is not ConstantValueExpr constant) {
      return false;
    }

    var rawValue = constant.ValueFn().AsRawNumber;
    if (rawValue == 0 && comparison.OperatorType == ComparisonOperator.OpType.Equal) {
      return true;
    }
    if (rawValue == 0 && comparison.OperatorType == ComparisonOperator.OpType.NotEqual) {
      state = true;
      return true;
    }
    if (rawValue == ScriptValue.FromBool(true).AsRawNumber
        && comparison.OperatorType == ComparisonOperator.OpType.Equal) {
      state = true;
      return true;
    }
    return false;
  }
}
