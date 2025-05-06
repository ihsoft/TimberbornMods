// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of an argument that can be passed to a script action or be returned from a signal.</summary>
sealed record ValueDef {
  /// <summary>The type of the argument.</summary>
  public ScriptValue.TypeEnum ValueType { get; init; }

  /// <summary>Optional formatting specification for the numbers.</summary>
  /// <remarks>If not provided, then the value is transformed to float and formatted with "0.##".</remarks>
  public string NumberFormat { get; init; }

  /// <summary>Optional validating function.</summary>
  /// <remarks>It should throw <see cref="ScriptError"/> if the value is not appropriate.</remarks>
  public Action<IValueExpr> ValueValidator { get; init; }

  /// <summary>Optional list of pre-defined values for the argument.</summary>
  public DropdownItem<string>[] Options { get; init; }
}
