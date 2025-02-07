// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Exception thrown when the script execution is interrupted.</summary>
/// <remarks>
/// This is not an error. This is a signal to stop executing the rule due to the current state is not right at the
/// moment, but it will be right later. Throwing this exception makes sense only if no side effect yet happens. For
/// example, in a conditions or in an action <i>before</i> it performed any changes.
/// </remarks>
class ExecutionInterrupted(string reason) : ScriptError("Execution interrupted: " + reason) {
  public readonly string Reason = reason;
}
