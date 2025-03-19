// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.Actions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>The component that keeps all the automation state on the building.</summary>
public sealed class AutomationBehavior : BaseComponent, IPersistentEntity, IDeletableEntity, IFinishedStateListener {

  #region Injection shortcuts

  /// <summary>Shortcut to the <see cref="AutomationService"/>.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public AutomationService AutomationService { get; private set; }

  /// <summary>Shortcut to the <see cref="ILoc"/>.</summary>
  public ILoc Loc => AutomationService.Loc;

  /// <summary>Shortcut to the <see cref="EventBus"/>.</summary>
  public EventBus EventBus => AutomationService.EventBus;

  /// <summary>Shortcut to the <see cref="BaseInstantiator"/>.</summary>
  public BaseInstantiator BaseInstantiator => AutomationService.BaseInstantiator;

  #endregion

  /// <summary>
  /// Automation can only work on the block objects. This is the object which this behavior is attached to.
  /// </summary>
  public BlockObject BlockObject { get; private set; }

  /// <summary>Indicates if there are any actions on the building.</summary>
  public bool HasActions => _actions.Count > 0;

  /// <summary>All actions on the building.</summary>
  public IList<IAutomationAction> Actions => _actions.AsReadOnly();
  List<IAutomationAction> _actions = [];

  #region API

  /// <summary>Creates a rule from the condition and action.</summary>
  /// <param name="condition">
  /// Condition definition. It will be owned by the behavior. Don't change or re-use it after adding.
  /// </param>
  /// <param name="action">
  /// Action definition. It will be owned by the behavior. Don't change or re-use it after adding.
  /// </param>
  public void AddRule(IAutomationCondition condition, IAutomationAction action) {
    action.Condition = condition;
    condition.Behavior = this;
    action.Behavior = this;
    condition.SyncState();
    _actions.Add(action);
    HostedDebugLog.Fine(this, "Adding rule: {0}", action);
    CollectCleanedRules();
    UpdateRegistration();
  }

  /// <summary>Deletes the specified rule.</summary>
  /// <see cref="Actions"/>
  public void DeleteRuleAt(int index) {
    var action = _actions[index];
    action.Condition.Behavior = null;
    action.Behavior = null;
    _actions.RemoveAt(index);
    UpdateRegistration();
  }

  /// <summary>Removes all rules defined for the specified template group.</summary>
  public void RemoveRulesForTemplateFamily(string templateFamily) {
    HostedDebugLog.Fine(this, "Removing all rules for template family: {0}", templateFamily);
    for (var i = _actions.Count - 1; i >= 0; i--) {
      var action = _actions[i];
      if (action.TemplateFamily == templateFamily) {
        DeleteRuleAt(i);
      }
    }
  }

  /// <summary>Removes all automation rules from the block object.</summary>
  public void ClearAllRules() {
    while (_actions.Count > 0) {
      DeleteRuleAt(0);
    }
  }

  /// <summary>Removes all rules that depend on condition and/or action that is marked for cleanup.</summary>
  public void CollectCleanedRules() {
    for (var i = _actions.Count - 1; i >= 0; i--) {
      var action = _actions[i];
      if (action.IsMarkedForCleanup || action.Condition.IsMarkedForCleanup) {
        HostedDebugLog.Fine(this, "Cleaning up action: {0}", action);
        DeleteRuleAt(i);
      }
    }
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
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    // Update rules that work on finished building only.
    foreach (var action in _actions) {
      if (!action.Behavior) {
        break;  // Not initialized yet. It is likely a save game load.
      }
      action.Condition.SyncState();
    }
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
  }

  #endregion

  #region IDeletableEntity implementation

  /// <inheritdoc/>
  public void DeleteEntity() {
    ClearAllRules();
  }

  #endregion

  #region Implementation

  /// <summary>Injects the dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(AutomationService automationService) {
    AutomationService = automationService;
  }

  void Awake() {
    BlockObject = GetComponentFast<BlockObject>();
  }

  void Start() {
    // This needs to be executed after all the entity components are loaded and initialized.
    foreach (var action in _actions) {
      action.Condition.Behavior = this;
      action.Behavior = this;
      action.Condition.SyncState();
    }
    CollectCleanedRules();
    UpdateRegistration();
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
