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
  public virtual string[] GetTriggerNamesForBuilding(BaseComponent building) => [];

  /// <inheritdoc/>
  public virtual ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual TriggerDef GetTriggerDefinition(string name, BaseComponent building) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual string[] GetActionNamesForBuilding(BaseComponent building) => [];

  /// <inheritdoc/>
  public virtual Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent instance) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public virtual ActionDef GetActionDefinition(string name, BaseComponent instance) {
    throw new NotImplementedException();
  }

  protected string LocTrigger(string name) {
    return Loc.T($"{ScriptableLocKeyPrefix}.{Name}.Trigger.{name}");
  }

  protected string LocAction(string name) {
    return Loc.T($"{ScriptableLocKeyPrefix}.{Name}.Action.{name}");
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
