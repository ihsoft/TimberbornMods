// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.Values;

/// <summary>Value that represents an array of values.</summary>
sealed class ArrayValue : IExpressionValue {

  readonly IExpressionValue[] _value;

  ArrayValue(IExpressionValue[] value) {
    _value = value;
  }

  /// <inheritdoc/>
  public IExpressionValue.ValueTypeEnum ValueType => IExpressionValue.ValueTypeEnum.Array;

  /// <summary>Creates array value from enumerable type.</summary>
  public static ArrayValue From(IEnumerable<IExpressionValue> value) {
    return new ArrayValue(value.ToArray());
  }

  /// <inheritdoc/>
  public int Compare(IExpressionValue other) => throw new System.NotImplementedException();

  /// <inheritdoc/>
  public string AsString() => $"[{string.Join(",", _value.Select(x => x.AsString()))}]";

  /// <inheritdoc/>
  public int AsNumber() => throw new ScriptError("Cannot convert array to number");

  /// <inheritdoc/>
  public bool AsBool() => throw new ScriptError("Cannot convert array to bool");

  /// <inheritdoc/>
  public object AsRawObject() => _value;

  /// <inheritdoc/>
  public IExpressionValue[] AsArray() => _value;

  /// <inheritdoc/>
  public override string ToString() => $"{ValueType}#{AsString()}";
}
