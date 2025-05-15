// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using TimberApi.DependencyContainerSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.StatusSystem;

namespace IgorZ.Automation.Actions;

/// <summary>This action only presents a status on the building. It doesn't do anything else.</summary>
/// <remarks>The other actions that need a status can inherit from this action.</remarks>
public class StatusToggleAction : AutomationActionBase {

  #region API
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable UnusedMember.Global

  /// <summary>All actions that this rule can perform.</summary>
  public enum ActionKindEnum {
    /// <summary>Shows the status for the requested <see cref="StatusToggleAction.StatusToken"/>.</summary>
    ShowStatus,
    /// <summary>Hides the status for the requested <see cref="StatusToggleAction.StatusToken"/>.</summary>
    HideStatus,
  }

  /// <summary>Specifies what this action should do to the blockable building.</summary>
  public ActionKindEnum ActionKind { get; private set;  }

  /// <summary>All status kinds that this action can present.</summary>
  /// <seealso cref="StatusToggle"/>
  public enum StatusKindEnum {
    /// <summary>Status that is only visible in the building UI panel when selected.</summary>
    NormalStatus,

    /// <summary>Status is visible on the UI panel and there is an icon above the building.</summary>
    NormalStatusWithFloatingIcon,

    /// <summary>Status in UI and floating icon and a message in the alerts panel.</summary>
    NormalStatusWithAlertAndFloatingIcon,

    /// <summary>
    /// Same as <see cref="NormalStatusWithFloatingIcon"/>, but this status will hide all the normal statuses.
    /// </summary>
    PriorityStatusWithFloatingIcon,

    /// <summary>
    /// Same as <see cref="NormalStatusWithAlertAndFloatingIcon"/>, but this status will hide all the normal statuses.
    /// </summary>
    PriorityStatusWithAlertAndFloatingIcon,
  }

  /// <summary>Indicates how the status should be presented.</summary>
  public StatusKindEnum StatusKind { get; private set; }

  /// <summary>Localization key for the text that describes the action in automation UI.</summary>
  public string Description { get; private set; }

  /// <summary>This is an identifier to bind the status to.</summary>
  /// <remarks>The show/hide actions must refer the same token to control the same status.</remarks>
  public string StatusToken { get; private set; }

  /// <summary>Specifies the icon of the status.</summary>
  public string StatusIcon { get; private set; }

  /// <summary>Localization key for the status text.</summary>
  public string StatusText { get; private set;  }

  /// <summary>Localization key for the alert text if the mode is .</summary>
  public string AlertText { get; private set;  }

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore UnusedMember.Global
  #endregion

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(Description);

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new StatusToggleAction() {
        StatusKind = StatusKind,
        Description = Description,
        StatusToken = StatusToken,
        ActionKind = ActionKind,
        StatusIcon = StatusIcon,
        StatusText = StatusText,
        AlertText = AlertText,
    };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    UpdateStatusState();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    UpdateStatusState();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    GetStatusController().HideStatus();
  }

  #endregion

  #region Implementation

  /// <summary>Returns existing blocker component or creates a new one.</summary>
  /// <remarks>
  /// On every behavior, there must be exactly one component per <see cref="StatusToken"/>. The blocker component is
  /// intentionally not created via Bindito. It would pollute the game objects with a component that most of them will
  /// never use. Thus, we create it dynamically only when needed.
  /// </remarks>
  StatusController GetStatusController() {
    var allBlockers = new List<StatusController>();
    Behavior.GetComponentsFast(allBlockers);
    var status = allBlockers.FirstOrDefault(x => x.StatusToken == StatusToken);
    if (!status) {
      var baseInstantiator = DependencyContainer.GetInstance<BaseInstantiator>();
      status = baseInstantiator.AddComponent<StatusController>(Behavior.GameObjectFast);
      status.SetStatusToken(StatusToken);
    }
    if (ActionKind == ActionKindEnum.ShowStatus) {
      // The blocker could get created from the hide action which doesn't have a status setting.
      status.SetStatus(this);
    }
    return status;
  }

  /// <summary>Ensures that the building's block state is in sync with the rule condition.</summary>
  void UpdateStatusState() {
    if (!Condition.ConditionState || IsMarkedForCleanup) {
      return;
    }
    if (ActionKind == ActionKindEnum.ShowStatus) {
      GetStatusController().ShowStatus();
    } else {
      GetStatusController().HideStatus();
    }
  }

  #endregion

  #region IGameSerializable implemenationsa

  static readonly PropertyKey<string> ActionKindKey = new("ActionKind");
  static readonly PropertyKey<string> DescriptionKey = new("Description");
  static readonly PropertyKey<string> StatusTokenKey = new("StatusToken");
  static readonly PropertyKey<string> StatusKindKey = new("StatusKind");
  static readonly PropertyKey<string> StatusIconKey = new("StatusIcon");
  static readonly PropertyKey<string> StatusTextKey = new("StatusText");
  static readonly PropertyKey<string> AlertTextKey = new("AlertText");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    ActionKind = (ActionKindEnum)Enum.Parse(typeof(ActionKindEnum), objectLoader.Get(ActionKindKey), ignoreCase: false);
    Description = objectLoader.Get(DescriptionKey);
    StatusToken = objectLoader.Get(StatusTokenKey);
    if (ActionKind == ActionKindEnum.HideStatus) {
      return;
    }
    StatusKind = (StatusKindEnum)Enum.Parse(typeof(StatusKindEnum), objectLoader.Get(StatusKindKey), ignoreCase: false);
    StatusIcon = objectLoader.Get(StatusIconKey);
    StatusText = objectLoader.Get(StatusTextKey);
    if (StatusKind == StatusKindEnum.NormalStatusWithAlertAndFloatingIcon
        || StatusKind == StatusKindEnum.PriorityStatusWithAlertAndFloatingIcon) {
      AlertText = objectLoader.Get(AlertTextKey);
    }
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ActionKindKey, ActionKind.ToString());
    objectSaver.Set(DescriptionKey, Description);
    objectSaver.Set(StatusTokenKey, StatusToken);
    if (ActionKind == ActionKindEnum.HideStatus) {
      return;
    }
    objectSaver.Set(StatusKindKey, StatusKind.ToString());
    objectSaver.Set(StatusIconKey, StatusIcon);
    objectSaver.Set(StatusTextKey, StatusText);
    if (StatusKind == StatusKindEnum.NormalStatusWithAlertAndFloatingIcon
        || StatusKind == StatusKindEnum.PriorityStatusWithAlertAndFloatingIcon) {
      objectSaver.Set(AlertTextKey, AlertText);
    }
  }

  #endregion

  #region Helper BaseComponent to show blocked status

  sealed class StatusController : BaseComponent {
    public string StatusToken { get; private set; }

    StatusToggle _statusToggle;
    ILoc _loc;

    [Inject]
    public void InjectDependencies(ILoc loc) {
      _loc = loc;
    }

    public void SetStatusToken(string statusTag) {
      StatusToken = statusTag;
    }

    public void SetStatus(StatusToggleAction toggleAction) {
      if (_statusToggle != null) {
        return;
      }
      _statusToggle = toggleAction.StatusKind switch {
          StatusKindEnum.NormalStatus =>
              StatusToggle.CreateNormalStatus(toggleAction.StatusIcon, _loc.T(toggleAction.StatusText)),
          StatusKindEnum.NormalStatusWithFloatingIcon =>
              StatusToggle.CreateNormalStatusWithFloatingIcon(toggleAction.StatusIcon, _loc.T(toggleAction.StatusText)),
          StatusKindEnum.NormalStatusWithAlertAndFloatingIcon =>
              StatusToggle.CreateNormalStatusWithAlertAndFloatingIcon(
                toggleAction.StatusIcon, _loc.T(toggleAction.StatusText), _loc.T(toggleAction.AlertText)),
          StatusKindEnum.PriorityStatusWithFloatingIcon =>
              StatusToggle.CreatePriorityStatusWithFloatingIcon(toggleAction.StatusIcon, _loc.T(toggleAction.StatusText)),
          StatusKindEnum.PriorityStatusWithAlertAndFloatingIcon =>
              StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
                toggleAction.StatusIcon, _loc.T(toggleAction.StatusText), _loc.T(toggleAction.AlertText)),
          _ => throw new InvalidDataException("Unknown status kind: " + toggleAction.StatusKind)
      };
      GetComponentFast<StatusSubject>().RegisterStatus(_statusToggle);
    }

    public void ShowStatus() {
      _statusToggle.Activate();
    }

    public void HideStatus() {
      // The toggle may not yet be created.
      _statusToggle?.Deactivate();
    }
  }

  #endregion
}