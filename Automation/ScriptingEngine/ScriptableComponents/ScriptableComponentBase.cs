// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

abstract class ScriptableComponentBase : ILoadableSingleton, IScriptable {

  const string ScriptableLocKeyPrefix = "IgorZ.Automation.Scriptable";

  #region API

  /// <inheritdoc/>
  public abstract string Name { get; }

  /// <inheritdoc/>
  public abstract Type InstanceType { get; }

  /// <inheritdoc/>
  public virtual ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual IScriptable.TriggerDef GetTriggerDefinition(string name, BaseComponent building) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual Action GetActionExecutor(string name, BaseComponent instance, string[] args) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual IScriptable.ActionDef GetActionDefinition(string name, BaseComponent instance) {
    throw new NotImplementedException();
  }

  protected string LocTrigger(string name) {
    return Loc.T($"{ScriptableLocKeyPrefix}.{Name}.Trigger.{name}");
  }

  protected string LocAction(string name) {
    return Loc.T($"{ScriptableLocKeyPrefix}.{Name}.Action.{name}");
  }

  protected static float ParseFloat(string value) {
    if (!int.TryParse(value, out var result)) {
      throw new ScriptError("Failed to parse number argument: " + value);
    }
    return result / 100f;
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
