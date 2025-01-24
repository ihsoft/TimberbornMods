// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of an argument that can be passed to a script action or be returned from a signal.</summary>
public sealed record ValueDef {
  /// <summary>The type of the argument.</summary>
  public ScriptValue.TypeEnum ValueType { get; init; }

  /// <summary>Optional formatting function for the number type of the constant value.</summary>
  /// <remarks>If not provided, then the value is transformed to float and formatted with "0.##".</remarks>
  public Func<int, string> FormatNumber { get; init; }

  /// <summary>Optional list of pre-defined values for the argument.</summary>
  public DropdownItem<string>[] Options { get; init; }
}
