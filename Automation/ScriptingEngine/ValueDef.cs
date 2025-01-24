// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of an argument that can be passed to a script action or be returned from a signal.</summary>
public sealed record ValueDef {
  /// <summary>The type of the argument.</summary>
  public ScriptValue.TypeEnum ValueType { get; init; }

  /// <summary>Optional formatting string.</summary>
  /// <remarks>
  /// Should be a one-argument formatting string, for example, "value={0.##}". If omitted, then simple "ToString" will
  /// be used to produce a string value.
  /// </remarks>
  public string Format { get; init; }

  /// <summary>Optional list of pre-defined values for the argument.</summary>
  public DropdownItem<string>[] Options { get; init; }
}
