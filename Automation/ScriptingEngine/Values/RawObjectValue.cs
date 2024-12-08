// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine.Values;

/// <summary>Value that wraps a raw C# object.</summary>
/// <remarks>
/// This vale is not expected to be exposed to the script. Thus, throw "hard" exceptions (that will crash the game) if
/// attempted to be used not properly.
/// </remarks>
sealed class RawObjectValue : IExpressionValue {

  readonly object _value;

  RawObjectValue(object value) {
    _value = value;
  }

  /// <inheritdoc/>
  public IExpressionValue.ValueTypeEnum ValueType => IExpressionValue.ValueTypeEnum.RawObject;

  /// <summary>Creates array value from enumerable type.</summary>
  public static RawObjectValue From(object value) => new RawObjectValue(value);

  /// <summary>Creates a "void" return result.</summary>
  public static RawObjectValue Void() => new RawObjectValue(null);

  /// <inheritdoc/>
  public int Compare(IExpressionValue other) => throw new NotImplementedException();

  /// <inheritdoc/>
  public string AsString()  => throw new NotImplementedException("Raw object must be used 'as-is'");

  /// <inheritdoc/>
  public int AsNumber() => throw new NotImplementedException("Raw object must be used 'as-is'");

  /// <inheritdoc/>
  public bool AsBool() => throw new NotImplementedException("Raw object must be used 'as-is'");

  /// <inheritdoc/>
  public object AsRawObject() => _value;

  /// <inheritdoc/>
  public IExpressionValue[] AsArray() => throw new NotImplementedException("Raw object must be used 'as-is'");

  /// <inheritdoc/>
  public override string ToString() => $"{ValueType}#{_value}";
}
