// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Automation.Actions;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Core {

public sealed class AutomationBehavior : BaseComponent, IPersistentEntity {
  #region Injection shortcuts
  public AutomationService AutomationService { get; private set; }
  public ILoc Loc => AutomationService.Loc;
  public EventBus EventBus => AutomationService.EventBus;
  public BaseInstantiator BaseInstantiator => AutomationService.BaseInstantiator;
  #endregion

  /// <summary>
  /// Automation can only work on the block objects. This is the object which this behavior is attached to.
  /// </summary>
  public BlockObject BlockObject { get; private set; }

  public bool HasActions => _actions.Count > 0;

  public IEnumerable<IAutomationAction> Actions => _actions.AsReadOnly();
  List<IAutomationAction> _actions = new();

  #region API
  public bool AddRule(IAutomationCondition condition, IAutomationAction action) {
    if (HasRule(condition, action)) {
      HostedDebugLog.Warning(this, "Skipping duplicate rule: condition={0}, action={1}", condition, action);
      return false;
    }
    action.Condition = condition;
    condition.Behavior = this;
    action.Behavior = this;
    condition.SyncState();
    if (action.IsMarkedForCleanup || condition.IsMarkedForCleanup) {
      HostedDebugLog.Fine(this, "Skipping rule that is marked for cleanup: {0}", action);
      return true;
    }
    _actions.Add(action);
    HostedDebugLog.Fine(this, "Adding rule: {0}", action);
    UpdateRegistration();
    return true;
  }

  public void ClearActions() {
    foreach (var action in _actions) {
      action.Condition.Behavior = null;
      action.Behavior = null;
    }
    _actions.Clear();
    UpdateRegistration();
  }

  public bool HasRule(IAutomationCondition condition, IAutomationAction action) {
    return _actions.Any(r => r.CheckSameDefinition(action) && r.Condition.CheckSameDefinition(condition));
  }
  #endregion

  #region IPersistentEntity implemenatation
  static readonly ComponentKey AutomationBehaviorKey = new(typeof(AutomationBehavior).FullName);
  static readonly ListKey<AutomationActionBase> ActionsKey = new("Actions");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    if (!HasActions) {
      return;
    }
    var component = entitySaver.GetComponent(AutomationBehaviorKey);
    var actionsToSave = _actions.OfType<AutomationActionBase>().ToList();
    component.Set(ActionsKey, actionsToSave, AutomationActionBase.ActionSerializer);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(AutomationBehaviorKey)) {
      return;
    }
    var component = entityLoader.GetComponent(AutomationBehaviorKey);
    _actions = component
        .Get(ActionsKey, AutomationActionBase.ActionSerializerNullable)
        .OfType<IAutomationAction>()
        .Where(a => !a.IsMarkedForCleanup && a.Condition is { IsMarkedForCleanup: false })
        .ToList();
    foreach (var action in _actions) {
      action.Condition.Behavior = this;
      action.Behavior = this;
    }
    UpdateRegistration();
  }
  #endregion

  #region Implementation
  [Inject]
  public void InjectDependencies(AutomationService automationService) {
    AutomationService = automationService;
  }

  void Start() {
    BlockObject = GetComponentFast<BlockObject>();
  }

  void OnDestroy() {
    ClearActions();
  }

  void UpdateRegistration() {
    if (HasActions) {
      AutomationService.RegisterBehavior(this);
    } else {
      AutomationService.UnregisterBehavior(this);
    }
  }
  #endregion
}

}
