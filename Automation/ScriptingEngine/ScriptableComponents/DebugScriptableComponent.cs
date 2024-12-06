// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Global objects for the debugging stuff.</summary>
sealed class DebugScriptableComponent : IScriptableInstance {

  /// <inheritdoc/>
  public string ScriptableTypeName => "Debug";

  #region Methods availabel to the scripts
  // ReSharper disable UnusedMember.Global

  [IScriptableInstance.ScriptFunction]
  public void Log(string message) {
    DebugEx.Info(message);
  }

  [IScriptableInstance.ScriptFunction]
  public void Log1(string message, string arg1) {
    DebugEx.Info(message, arg1);
  }

  // ReSharper enable UnusedMember.Global
  #endregion
}
