// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngine;

record struct ArgumentDef {
  public ScriptValue.TypeEnum ValueType { get; init; }
  public string Format { get; init; }
  public DropdownItem<string>[] Options { get; init; }
}
