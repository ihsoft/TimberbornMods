// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

class ExecutionInterrupted(string reason) : ScriptError("Execution interrupted: " + reason) {
  public readonly string Reason = reason;
}
