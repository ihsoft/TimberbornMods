// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Automation.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.Emptying;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Actions {

/// <summary>Action that turns the storage into the empty mode.</summary>
/// <remarks>
/// This action disables empty mode when disconnected from the automation behavior because not evey provides an has
/// explicit control over this mode.
/// </remarks>
public sealed class MarkForEmptyingAction : AutomationActionBase {
  const string DescriptionLocKey = "IgorZ.Automation.MarkForEmptyingAction.Description";
  const string CustomStatusIcon = "igorz.automation/ui_icons/status-icon-emptying";
  const string CustomStatusDescriptionKey = "IgorZ.Automation.EmptyOutputStore.CustomStatus";

  /// <summary>
  /// Indicates that emptying mode was initiated by the action and there is a customs status icon being shown.
  /// </summary>
  public bool ShowCustomStatus { get; private set; } 

  #region AutomationActionBase overrides
  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new MarkForEmptyingAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return behavior.GetComponentFast<Emptiable>() != null;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    var component = Behavior.GetComponentFast<Emptiable>();
    if (component.IsMarkedForEmptying) {
      DebugEx.Fine("Don't mark for emptying as it's already emptying: {0}", Behavior);
      return;
    }
    DebugEx.Fine("Mark for emptying: {0}", Behavior);
    component.MarkForEmptyingWithoutStatus();
    ActivateCustomStatus();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    if (ShowCustomStatus) {
      ActivateCustomStatus();
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    if (!ShowCustomStatus) {
      return;
    }
    var component = Behavior.GetComponentFast<Emptiable>();
    if (component.IsMarkedForEmptying) {
      component.UnmarkForEmptying();
    }
  }
  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<bool> ShowStatusKey = new(nameof(ShowCustomStatus));

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    ShowCustomStatus = objectLoader.Has(ShowStatusKey) && objectLoader.Get(ShowStatusKey);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ShowStatusKey, ShowCustomStatus);
  }
  #endregion

  #region Helper behavior class
  /// <summary>
  /// Creates a custom status icon that indicates that the storage is being emptying. If the status is changed
  /// externally, then hides the status and notifies the action.
  /// </summary>
  sealed class CustomStatusBehavior : BaseComponent {
    StatusToggle _statusToggle;
    public event EventHandler<EventArgs> StatusReset;

    void Start() {
      _statusToggle = StatusToggle.CreatePriorityStatusWithFloatingIcon(
          CustomStatusIcon,
          GetComponentFast<AutomationBehavior>().Loc.T(CustomStatusDescriptionKey));
      GetComponentFast<Emptiable>().UnmarkedForEmptying += OnUnmarkedForEmptying;
      var subject = GetComponentFast<StatusSubject>();
      subject.RegisterStatus(_statusToggle);
      _statusToggle.Activate();
    }

    void OnUnmarkedForEmptying(object sender, EventArgs args) {
      _statusToggle.Deactivate();
      StatusReset?.Invoke(this, EventArgs.Empty);
      Destroy(this);
    }
  }
  #endregion

  #region Implementation
  void ActivateCustomStatus() {
    ShowCustomStatus = true;
    var status = Behavior.BaseInstantiator.AddComponent<CustomStatusBehavior>(Behavior.GameObjectFast);
    status.StatusReset += (sender, args) => ShowCustomStatus = false;
  }
  #endregion
}

}
