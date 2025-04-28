// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

abstract class ScriptableComponentBase : ILoadableSingleton, IScriptable {

  #region API

  /// <inheritdoc/>
  public abstract string Name { get; }

  /// <inheritdoc/>
  public virtual string[] GetSignalNamesForBuilding(BaseComponent building) => [];

  /// <inheritdoc/>
  public virtual Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    throw new ScriptError.ParsingError("Signal not found: " + name);
  }

  /// <inheritdoc/>
  public virtual SignalDef GetSignalDefinition(string name, BaseComponent building) {
    throw new ScriptError.ParsingError("Signal not found: " + name);
  }

  /// <inheritdoc/>
  public virtual string[] GetActionNamesForBuilding(BaseComponent building) => [];

  /// <inheritdoc/>
  public virtual Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    throw new ScriptError.ParsingError("Action not found: " + name);
  }

  /// <inheritdoc/>
  public virtual ActionDef GetActionDefinition(string name, BaseComponent building) {
    throw new ScriptError.ParsingError("Action not found: " + name);
  }

  /// <inheritdoc/>
  public virtual void RegisterSignalChangeCallback(string name, ISignalListener host) {
    throw new ScriptError.ParsingError("Unknown signal: " + name);
  }

  /// <inheritdoc/>
  public virtual void UnregisterSignalChangeCallback(string name, ISignalListener host) {
  }

  /// <inheritdoc/>
  public virtual void InstallAction(string name, BaseComponent building) {
  }

  /// <inheritdoc/>
  public virtual void UninstallAction(string name, BaseComponent building) {
  }

  protected static void AssertActionArgsCount(string actionName, ScriptValue[] args, int expectedCount) {
    if (args.Length != expectedCount) {
      throw new ScriptError.ParsingError($"{actionName} action requires {expectedCount} argument(s)");
    }
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public virtual void Load() {
    DebugEx.Fine("Registering scriptable component: {0}", GetType().FullName);
    ScriptingService.RegisterScriptable(this);
  }

  #endregion

  #region Implemenation

  protected ILoc Loc { get; private set; }
  protected ScriptingService ScriptingService { get; private set; }

  [Inject]
  public void InjectDependencies(ILoc loc, ScriptingService scriptingService) {
    Loc = loc;
    ScriptingService = scriptingService;
  }

  #endregion
}
