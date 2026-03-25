// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

abstract class ScriptableComponentBase : ILoadableSingleton, IScriptable {

  const string ArgumentMaxValueHintLocKey = "IgorZ.Automation.Scripting.Editor.ArgumentMaxValueHint";

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

  protected string GetArgumentMinMaxValueHint(int minValue, int maxValue) {
    return maxValue == int.MaxValue ? null : $"({minValue}..{maxValue})";
  }

  protected string GetArgumentMaxValueHint(int maxValue) {
    return maxValue == int.MaxValue ? null : Loc.T(ArgumentMaxValueHintLocKey, maxValue.ToString());
  }

  protected string GetArgumentMaxValueHint(float maxValue, string format = "F2") {
    return maxValue < 0 ? null : Loc.T(ArgumentMaxValueHintLocKey, maxValue.ToString(format));
  }

  /// <summary>Returns the requested component or throws a parsing error if not found.</summary>
  /// <remarks>
  /// Use this method in the parsing flow where signals or actions may be requested on an incompatible building.
  /// </remarks>
  /// <exception cref="ScriptError.BadStateError">if the component not found.</exception>
  protected static T GetComponentOrThrow<T>(AutomationBehavior behavior) where T : BaseComponent {
    var component = behavior.GetComponent<T>();
    return component ?? throw new ScriptError.BadStateError(behavior, $"Building has no '{typeof(T).Name}'");
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
