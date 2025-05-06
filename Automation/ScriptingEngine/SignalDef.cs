// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Definition of a signal that can be used in the scripting engine.</summary>
sealed record SignalDef {
  /// <summary>Unique name of the signal as it appears in the scripts.</summary>
  public string ScriptName { get; init; }

  /// <summary>Human-readable and localized name of the signal.</summary>
  public string DisplayName { get; init; }

  /// <summary>Definition of the result value.</summary>
  public ValueDef Result { get; init; }
}
