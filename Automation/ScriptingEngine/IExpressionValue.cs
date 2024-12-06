// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

interface IExpressionValue {
  /// <summary>The supported types.</summary>
  public enum ValueTypeEnum {
    /// <summary>String literal.</summary>
    String,
    /// <summary>2-digits precision fixed point float.</summary>
    Number,
    /// <summary>Boolean.</summary>
    Bool,
  }

  /// <summary>The original type of the node's value. All other values will be converted from this type.</summary>
  public ValueTypeEnum ValueType { get; }

  /// <summary>Compares the node with another node.</summary>
  /// <remarks>If the types are different, then rhe type of <paramref name="other"/> is cast to the left node.</remarks>
  public int Compare(IExpressionValue other);

  /// <summary>
  /// Converts the value to string. 
  /// </summary>
  /// <remarks>
  /// When casting from another type, the string representation should be reversible. In other words, the string value
  /// should be parsable back to the original type.
  /// </remarks>
  public string AsString();

  /// <summary>
  /// Returns a 2-digits fixed point float. That is, the value is multiplied by 100 and rounded to the nearest integer.
  /// </summary>
  /// <remarks>
  /// When cast from boolean, gives '100' (1.0 in fixed-point format) for 'true', and '0' for 'false'.
  /// </remarks>
  public int AsNumber();

  /// <summary>Returns a boolean value.</summary>
  /// <remarks>
  /// When parsed from a string, recognizes 'true/false' keywords case-insensitive, or uses the number casting rules
  /// otherwise. When cast from a number, then anything that is not '0' is 'true', otherwise – 'false'.  
  /// </remarks>
  public bool AsBool();
}
