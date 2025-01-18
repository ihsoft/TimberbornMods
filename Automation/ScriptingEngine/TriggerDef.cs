// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

record struct TriggerDef {
  public string FullName { get; init; }
  public string DisplayName { get; init; }
  public ArgumentDef ResultType { get; init; }
}
