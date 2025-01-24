// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

public record struct ActionDef {
  public string FullName { get; init; }
  public string DisplayName { get; init; }
  public ArgumentDef[] ArgumentTypes { get; init; }
}
