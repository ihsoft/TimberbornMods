// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Global objects for the debugging stuff.</summary>
sealed class DebugScriptableComponent : IScriptableInstance {

  /// <inheritdoc/>
  public string ScriptableTypeName => ComponentsIndex.TypeToName[typeof(DebugScriptableComponent)];

  #region Methods available to the scripts
  // ReSharper disable UnusedMember.Global

  [IScriptableInstance.ScriptFunction]
  public void Log(IExpressionValue message) {
    DebugEx.Info(message.AsString());
  }

  [IScriptableInstance.ScriptFunction]
  public void Log1(IExpressionValue message, IExpressionValue arg1) {
    DebugEx.Info(message.AsString(), arg1.AsRawObject());
  }

  // ReSharper enable UnusedMember.Global
  #endregion
}
