// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of an action that can be executed by a script.</summary>
public record ActionDef {
  /// <summary>Unique name of the action as it appears in the scripts.</summary>
  public string ScriptName { get; init; }

  /// <summary>Human-readable and localized name of the action.</summary>
  public string DisplayName { get; init; }

  /// <summary>The arguments that the action expects.</summary>
  public ValueDef[] Arguments { get; init; }
}
