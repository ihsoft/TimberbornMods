// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
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
public sealed class AutomationBehavior : BaseComponent, IPersistentEntity, IDeletableEntity {

  const string AutomationErrorIcon = "IgorZ.Automation/error-icon-script-failed";
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

  #region API

  /// <summary>
  /// Automation can only work on the block objects. This is the object which this behavior is attached to.
  /// </summary>
  public BlockObject BlockObject { get; private set; }

  /// <summary>Indicates if there are any actions on the building.</summary>
  public bool HasActions => _actions.Count > 0;

  /// <summary>All actions on the building.</summary>
  public IList<IAutomationAction> Actions => _actions.AsReadOnly();
  List<IAutomationAction> _actions = [];

  /// <summary>The version is updated each time the behavior is changed.</summary>
  /// <remarks>It is a monotonically increasing value. Old versions have a less value than the newer versions.</remarks>
  /// <seealso cref="IncrementStateVersion"/>
  public int StateVersion { get; private set; }

  /// <summary>Increments the state version.</summary>
  /// <remarks>
  /// Update the version each time the behavior changes in a way that can be important for the stateful logic. For
  /// example, UI can check it to avoid constant refreshes.
  /// </remarks>
  /// <seealso cref="StateVersion"/>
  public void IncrementStateVersion() {
    StateVersion++;
  }

  /// <summary>Creates a rule from the condition and action.</summary>
  /// <param name="condition">
  /// Condition definition. It will be owned by the behavior. Don't change or re-use it after adding.
  /// </param>
  /// <param name="action">
  /// Action definition. It will be owned by the behavior. Don't change or re-use it after adding.
  /// </param>
  public void AddRule(IAutomationCondition condition, IAutomationAction action) {
    action.Condition = condition;
    HostedDebugLog.Fine(this, "Adding rule: action={0}", action);
    condition.Behavior = this;
    action.Behavior = this;
    if (BlockObject.IsFinished || condition.CanRunOnUnfinishedBuildings) {
      condition.SyncState(force: true);
    }
    _actions.Add(action);
    IncrementStateVersion();
    UpdateRegistration();
  }

  /// <summary>Deletes the specified rule.</summary>
  /// <see cref="Actions"/>
  public void DeleteRuleAt(int index) {
    var action = _actions[index];
    HostedDebugLog.Fine(this, "Deleting rule at index {0}: action={1}", index, _actions[index]);
    _actions.RemoveAt(index);
    action.Condition.Behavior = null;
    action.Behavior = null;
    IncrementStateVersion();
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

  StatusToggle _errorToggle;
  readonly HashSet<object> _failingInstances = [];

  /// <summary>Shows an error status for a building that has problems with the rules.</summary>
  public void ReportError(object instance) {
    HostedDebugLog.Error(this, "Automation error reported by: {0}", instance);
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

  /// <summary>Returns the component or creates it if none exists.</summary>
  public T GetOrCreate<T>() where T : BaseComponent {
    return GetComponentFast<T>() ?? BaseInstantiator.AddComponent<T>(GameObjectFast);
  }

  /// <summary>Returns the component or throws an exception if none exists.</summary>
  /// <exception cref="InvalidOperationException">if the requested component not found.</exception>
  public T GetOrThrow<T>() where T : BaseComponent {
    var tracker = GetComponentFast<T>();
    if (!tracker) {
      throw new InvalidOperationException($"Component {typeof(T).Name} not found");
    }
    return tracker;
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
    UpdateRegistration();
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

  /// <summary>Removes all rules that depend on condition and/or action that is marked for cleanup.</summary>
  internal void CollectCleanedRules() {
    for (var i = _actions.Count - 1; i >= 0; i--) {
      var action = _actions[i];
      if (action.IsMarkedForCleanup || action.Condition.IsMarkedForCleanup) {
        HostedDebugLog.Fine(this, "Cleaning up action: {0}", action);
        DeleteRuleAt(i);
      }
    }
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
