// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.Actions;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.DuplicationSystem;
using Timberborn.EntitySystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>The component that keeps all the automation state on the building.</summary>
public sealed class AutomationBehavior : BaseComponent, IAwakableComponent, IInitializableEntity,
                                         IFinishedStateListener, IStartableComponent, IPersistentEntity,
                                         IDeletableEntity, IDuplicable<AutomationBehavior> {

  const string AutomationErrorIcon = "IgorZ.Automation/error-icon-script-failed";
  const string AutomationErrorAlertLocKey = "IgorZ.Automation.ShowStatusAction.AutomationErrorAlert";
  const string AutomationErrorDescriptionLocKey = "IgorZ.Automation.ShowStatusAction.AutomationErrorDescription";

  #region Injection shortcuts

  /// <summary>Shortcut to the <see cref="AutomationService"/>.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public AutomationService AutomationService { get; private set; }

  /// <summary>Shortcut to the <see cref="ILoc"/>.</summary>
  public ILoc Loc => AutomationService.Loc;

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
    if (condition.IsEnabled && (BlockObject.IsFinished || condition.CanRunOnUnfinishedBuildings)) {
      condition.Activate();
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

  /// <summary>Deletes the specified rule.</summary>
  /// <see cref="Actions"/>
  /// <exception cref="InvalidOperationException">if the specified action is not found in the behavior.</exception>
  public void DeleteRule(IAutomationAction action) {
    var index = _actions.IndexOf(action);
    if (index < 0) {
      throw new InvalidOperationException("The specified action is not found in the behavior");
    }
    DeleteRuleAt(index);
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
    for (var i = _actions.Count - 1; i >= 0; i--) {
      DeleteRuleAt(i);
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
      GetComponent<StatusSubject>().RegisterStatus(_errorToggle);
    }
    _errorToggle.Activate();
    _failingInstances.Add(instance);
    IncrementStateVersion();
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
    IncrementStateVersion();
  }

  readonly Dictionary<Type, AbstractDynamicComponent> _dynamicComponents = [];

  /// <summary>Returns the component or creates it if none exists.</summary>
  /// <remarks>
  /// The newly created components will receive all callbacks that they would receive if were created at the behavior
  /// creation time. Callbacks sequence: Awake, Start (if enabled), OnEnterFinishedState (if finished),
  /// InitializeEntity (if initialized).
  /// </remarks>
  public T GetOrCreate<T>() where T : AbstractDynamicComponent {
    if (_dynamicComponents.TryGetValue(typeof(T), out var component)) {
      return (T)component;
    }
    return (T)CreateDynamicComponent(typeof(T));
  }

  /// <summary>Returns the component or throws an exception if none exists.</summary>
  /// <exception cref="InvalidOperationException">if the requested component not found.</exception>
  public T GetOrThrow<T>() where T : AbstractDynamicComponent {
    if (_dynamicComponents.TryGetValue(typeof(T), out var component)) {
      return (T)component;
    }
    throw new InvalidOperationException($"Component {typeof(T).Name} not found");
  }

  /// <summary>Verifies if the dynamic component exists and returns it.</summary>
  public bool TryGetDynamicComponent<T>(out T component) where T : AbstractDynamicComponent {
    if (_dynamicComponents.TryGetValue(typeof(T), out var abstractComponent)) {
      component = (T)abstractComponent;
      return true;
    }
    component = null;
    return false;
  }

  /// <summary>Returns the requested component or crashes the game.</summary>
  /// <remarks>Use this method in the logic where the component is normally expected to exist.</remarks>
  /// <exception cref="InvalidOperationException">if teh component not found.</exception>
  /// <seealso cref="BaseComponent.GetComponent"/>
  public T GetComponentOrFail<T>() where T : BaseComponent {
    return GetComponent<T>()
        ?? throw new InvalidOperationException($"Cannot find {typeof(T).FullName} on {DebugEx.ObjectToString(this)}");
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey AutomationBehaviorKey = new(typeof(AutomationBehavior).FullName);
  static readonly ListKey<AutomationActionBase> ActionsKey = new("Actions");
  static readonly ListKey<string> SavedComponentsKey = new("SavedComponents");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    if (!HasActions) {
      return;
    }
    var component = entitySaver.GetComponent(AutomationBehaviorKey);
    var actionsToSave = _actions.OfType<AutomationActionBase>().ToList();
    component.Set(ActionsKey, actionsToSave, AutomationActionBase.ActionSerializer);

    var savedComponentTypes = new List<string>();
    foreach (var persistentDynamicComponent in GetDynamicComponentsOf<IPersistentEntity>()) {
      savedComponentTypes.Add(persistentDynamicComponent.GetType().FullName);
      persistentDynamicComponent.Save(entitySaver);
    }
    if (savedComponentTypes.Count > 0) {
      component.Set(SavedComponentsKey, savedComponentTypes);
    }
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

    if (!component.Has(SavedComponentsKey)) {
      return;
    }
    var savedComponentTypes = component.Get(SavedComponentsKey);
    foreach (var savedComponentType in savedComponentTypes) {
      var componentType = ReflectionsHelper.GetType(savedComponentType, baseType: typeof(AbstractDynamicComponent),
                                                    needDefaultConstructor: false, throwOnError: false);
      if (componentType == null) {
        HostedDebugLog.Error(this, "Cannot make dynamic component type: {0}", savedComponentType);
        continue;
      }
      var abstractComponent = CreateDynamicComponent(componentType);
      if (abstractComponent is not IPersistentEntity persistentEntity) {
        HostedDebugLog.Error(this, "Dynamic component type {0} is not a persistent entity", componentType.FullName);
        continue;
      }
      persistentEntity.Load(entityLoader);
    }
  }

  #endregion

  #region IDeletableEntity implementation

  /// <inheritdoc/>
  public void DeleteEntity() {
    foreach (var component in GetDynamicComponentsOf<IDeletableEntity>()) {
      component.DeleteEntity();
    }
    ClearAllRules();
  }

  #endregion

  #region IAwakableComponent implementation
  
  /// <inheritdoc/>
  public void Awake() {
    BlockObject = GetComponent<BlockObject>();
  }

  #endregion

  #region IInitializableEntity implementation

  /// <inheritdoc/>
  public void InitializeEntity() {
    _isInitialized = true;
    foreach (var component in GetDynamicComponentsOf<IInitializableEntity>()) {
      component.InitializeEntity();
    }
  }
  
  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    foreach (var listener in GetDynamicComponentsOf<IFinishedStateListener>()) {
      listener.OnEnterFinishedState();
    }
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    foreach (var listener in GetDynamicComponentsOf<IFinishedStateListener>()) {
      listener.OnExitFinishedState();
    }
  }

  #endregion

  #region IStartableComponent implementation

  /// <inheritdoc/>
  public void Start() {
    foreach (var component in _dynamicComponents.Values) {
      component.Start();
    }
  }

  #endregion

  #region IDuplicable implementation

  /// <inheritdoc/>
  public bool IsDuplicable => HasActions;

  /// <inheritdoc/>
  public void DuplicateFrom(AutomationBehavior source) {
    if (source.Actions.Count == 0 || Name != source.Name) {
      return;
    }
    AutomationService.Instance.ScheduleLateUpdateOnce("duplication", () => {
      HostedDebugLog.Info(this, "Duplicating {0} rules from {1}", source.Actions.Count, source);
      ClearAllRules();
      foreach (var action in source.Actions) {
        AddRule(action.Condition.CloneDefinition(), action.CloneDefinition());
      }
    });
  }

  #endregion

  #region Implementation

  bool _isInitialized;

  /// <summary>Injects the dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(AutomationService automationService) {
    AutomationService = automationService;
  }

  IEnumerable<T> GetDynamicComponentsOf<T>() {
    return _dynamicComponents
        .Where(x => typeof(T).IsAssignableFrom(x.Key))
        .Select(x => x.Value)
        .Cast<T>();
  }

  AbstractDynamicComponent CreateDynamicComponent(Type type) {
    var component = (AbstractDynamicComponent)StaticBindings.DependencyContainer.GetInstance(type);
    _dynamicComponents.Add(type, component);
    component.Initialize(this);
    if (component is IAwakableComponent awakableComponent) {
      awakableComponent.Awake();
    }
    if (component.Enabled && _componentCache.StartIsEnabled) {
      component.Start();
    }
    if (BlockObject.IsFinished && component is IFinishedStateListener finishedStateListener) {
      finishedStateListener.OnEnterFinishedState();
    }
    if (_isInitialized && component is IInitializableEntity initializableEntity) {
      initializableEntity.InitializeEntity();
    }
    return component;
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
