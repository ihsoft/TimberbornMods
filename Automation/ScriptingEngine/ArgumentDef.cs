// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngine;

record struct ArgumentDef {
  public ScriptValue.TypeEnum ValueType { get; init; }
  public string Units { get; init; }
  //FIXME: don't fill it on every get def call since it can be expensive.
  public DropdownItem<string>[] Options { get; init; }
}
