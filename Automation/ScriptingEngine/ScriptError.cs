// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Exception that is thrown when a script error occurs.</summary>
/// <remarks>Such errors don't fail the game, but they cancel any script execution on the building.</remarks>
class ScriptError : Exception {
  /// <summary>Creates a new instance of the exception.</summary>
  public ScriptError(string message) : base(message) {}
}
