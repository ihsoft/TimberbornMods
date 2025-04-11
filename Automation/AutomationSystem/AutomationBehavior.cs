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
using Timberborn.StatusSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>The component that keeps all the automation state on the building.</summary>
public sealed class AutomationBehavior : BaseComponent, IPersistentEntity, IDeletableEntity, IFinishedStateListener {

  //FXIME: Make own icon!
  const string AutomationErrorIcon = "NoPower";
  const string AutomationErrorAlertLocKey = "IgorZ.Automation.ShowStatusAction.AutomationErrorAlert";
  const string AutomationErrorDescriptionLocKey = "IgorZ.Automation.ShowStatusAction.AutomationErrorDescription";

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

  StatusToggle _errorToggle;
  readonly HashSet<object> _failingInstances = [];

  /// <summary>Shows an error status for a building that has problems with the rules.</summary>
  public void ReportError(object instance) {
    HostedDebugLog.Fine(this, "Automation error reported by: {0}", instance);
    if (_errorToggle == null) {
      _errorToggle = StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
          AutomationErrorIcon, Loc.T(AutomationErrorDescriptionLocKey), Loc.T(AutomationErrorAlertLocKey));
      GetComponentFast<StatusSubject>().RegisterStatus(_errorToggle);
    }
    _errorToggle.Activate();
    _failingInstances.Add(instance);
  }

  /// <summary>Clears the error status.</summary>
  public void ClearError(object instance) {
    if (!_failingInstances.Remove(instance)) {
      DebugEx.Warning("Cannot clear error. It was never reported by: {0}", instance);
      return;
    }
    HostedDebugLog.Fine(this, "Clearing error from: {0}", instance);
    if (_failingInstances.Count == 0) {
      _errorToggle?.Deactivate();
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
    if (!entityLoader.TryGetComponent(AutomationBehaviorKey, out var component)) {
      return;
    }
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
    CollectCleanedRules();
    UpdateRegistration();
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
