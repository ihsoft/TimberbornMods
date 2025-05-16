// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

abstract class ScriptableComponentBase : ILoadableSingleton, IScriptable {

  protected const string ArgumentMaxValueHintLocKey = "IgorZ.Automation.Scripting.Editor.ArgumentMaxValueHint";

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
  public virtual Func<object> GetPropertySource(string name, AutomationBehavior behavior) {
    return null;
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

  #endregion

  #region Internal API

  protected static void AssertActionArgsCount(string actionName, ScriptValue[] args, int expectedCount) {
    if (args.Length != expectedCount) {
      throw new ScriptError.ParsingError($"{actionName} action requires {expectedCount} argument(s)");
    }
  }

  protected ActionDef LookupActionDef(string name, Func<ActionDef> getDefault) {
    if (!_cachedActionDefs.TryGetValue(name, out var actionDef)) {
      actionDef = getDefault();
      _cachedActionDefs[name] = actionDef;
      DebugEx.Fine("Registering action in cache:\n{0}", actionDef);
    }
    return actionDef;
  }
  readonly Dictionary<string, ActionDef> _cachedActionDefs = new();

  protected SignalDef LookupSignalDef(string name, Func<SignalDef> getDefault) {
    if (!_cachedSignalDefs.TryGetValue(name, out var signalDef)) {
      signalDef = getDefault();
      _cachedSignalDefs[name] = signalDef;
      DebugEx.Fine("Registering signal in cache:\n{0}", signalDef);
    }
    return signalDef;
  }
  readonly Dictionary<string, SignalDef> _cachedSignalDefs = new();

  protected string GetArgumentMaxValueHint(int maxValue) {
    return maxValue == int.MaxValue ? null : Loc.T(ArgumentMaxValueHintLocKey, maxValue.ToString());
  }

  protected string GetArgumentMaxValueHint(float maxValue, string format = "F2") {
    return maxValue < 0 ? null : Loc.T(ArgumentMaxValueHintLocKey, maxValue.ToString(format));
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
