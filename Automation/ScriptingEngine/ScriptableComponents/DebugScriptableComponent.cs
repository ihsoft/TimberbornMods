// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class DebugScriptableComponent : IScriptableInstance {

  /// <inheritdoc/>
  public string ScriptableTypeName => "Debug";

  [IScriptableInstance.ScriptFunction]
  public void Log(string message) {
    DebugEx.Info(message);
  }

  [IScriptableInstance.ScriptFunction]
  public void Log1(string message, string arg1) {
    DebugEx.Info(message, arg1);
  }
}

