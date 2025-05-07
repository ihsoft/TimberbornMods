// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

abstract class ScriptableComponentBase : ILoadableSingleton, IScriptable {

  #region API

  /// <summary>As script processing exception for the missing signal.</summary>
  /// <remarks>This exception must be handled by the caller and not crash the game.</remarks>
  protected class UnknownSignalException(string signalName) : ScriptError.ParsingError("Unknown signal: " + signalName);
  
  /// <summary>As script processing exception for the missing action.</summary>
  /// <remarks>This exception must be handled by the caller and not crash the game.</remarks>
  protected class UnknownActionException(string actionName) : ScriptError.ParsingError("Unknown action: " + actionName);

  /// <inheritdoc/>
  public abstract string Name { get; }

  /// <inheritdoc/>
  public virtual string[] GetSignalNamesForBuilding(AutomationBehavior behavior) => [];

  /// <inheritdoc/>
  public virtual Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    throw new UnknownSignalException(name);
  }

  /// <inheritdoc/>
  public virtual SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    throw new UnknownSignalException(name);
  }

  /// <inheritdoc/>
  public virtual Func<object> GetPropertySource(string name, BaseComponent component) {
    throw new ScriptError.ParsingError("Property not found: " + name);
  }

  /// <inheritdoc/>
  public virtual string[] GetActionNamesForBuilding(AutomationBehavior behavior) => [];

  /// <inheritdoc/>
  public virtual Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    throw new UnknownActionException(name);
  }

  /// <inheritdoc/>
  public virtual ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    throw new UnknownActionException(name);
  }

  /// <inheritdoc/>
  public virtual void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
  }

  /// <inheritdoc/>
  public virtual void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
  }

  /// <inheritdoc/>
  public virtual void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
  }

  /// <inheritdoc/>
  public virtual void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
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
