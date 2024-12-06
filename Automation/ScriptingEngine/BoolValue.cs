// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

sealed class BoolValue(bool value) : IExpressionValue {

  /// <inheritdoc/>
  public IExpressionValue.ValueTypeEnum ValueType => IExpressionValue.ValueTypeEnum.Bool;

  /// <summary>Creates number value from int.</summary>
  public static BoolValue FromBool(bool value) {
    return new BoolValue(value);
  }

  /// <inheritdoc/>
  public int Compare(IExpressionValue other) {
    return value.CompareTo(other.AsBool());
  }

  /// <inheritdoc/>
  public string AsString() => value.ToString();

  /// <inheritdoc/>
  public int AsNumber()  => value ? 100 : 0;

  /// <inheritdoc/>
  public bool AsBool() => value;

  /// <inheritdoc/>
  public override string ToString() => $"{ValueType}#{AsString()}";
}