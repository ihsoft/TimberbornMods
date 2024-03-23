// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BuilderHubSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.StatusSystem;

namespace Automation.PathCheckingSystem {

/// <summary>Helper component that monitors site state and displays status information.</summary>
sealed class PathCheckingSiteStatusTracker : BaseComponent, ISelectionListener {
  const string NotYetReachableLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotYetReachable";
  const string UnreachableStatusLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableStatus";
  const string UnreachableAlertLocKey = "IgorZ.Automation.CheckAccessBlockCondition.UnreachableAlert";
  const string UnreachableIconName = "UnreachableObject";

  StatusToggle _unreachableStatusToggle;
  StatusToggle _maybeReachableStatusToggle;
  BlockableBuilding _blockableBuilding;
  BuilderJobReachabilityStatus _builderJobReachabilityStatus;
  bool _isSelected;

  void Awake() {
    _blockableBuilding = GetComponentFast<BlockableBuilding>();
    _builderJobReachabilityStatus = GetComponentFast<BuilderJobReachabilityStatus>();
  }

  /// <summary>Initializes the component since the normal Bindito logic doesn't work here.</summary>
  public void Initialize(ILoc loc) {
    _unreachableStatusToggle = StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(
        UnreachableIconName, loc.T(UnreachableStatusLocKey), loc.T(UnreachableAlertLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_unreachableStatusToggle);
    _maybeReachableStatusToggle = StatusToggle.CreateNormalStatus(UnreachableIconName, loc.T(NotYetReachableLocKey));
    GetComponentFast<StatusSubject>().RegisterStatus(_maybeReachableStatusToggle);
  }

  /// <summary>
  /// The unreachable sites cannot be built.
  /// </summary>
  /// <remarks>
  /// Our meaning of "unreachable" can be different from the game's point of view. For the algorithm, it's important
  /// to not have such sites started until we allow it to. Thus, we block such sites.
  /// </remarks>
  public void SetUnreachable() {
    _unreachableStatusToggle.Activate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableBuilding.Block(this);
  }

  public void SetMaybeReachable() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Activate();
    _blockableBuilding.Block(this);
  }

  /// <summary>A cleanup method that resets all effects on the site.</summary>
  public void Cleanup() {
    _unreachableStatusToggle.Deactivate();
    _maybeReachableStatusToggle.Deactivate();
    _blockableBuilding.Unblock(this);
    if (_isSelected && _builderJobReachabilityStatus) {
      _builderJobReachabilityStatus.OnSelect();
    }
  }

  #region ISelectionListener implemenation

  /// <inheritdoc/>
  public void OnSelect() {
    _isSelected = true;
    if (!_builderJobReachabilityStatus) {
      return;
    }
    if (_unreachableStatusToggle.IsActive || _maybeReachableStatusToggle.IsActive) {
      _builderJobReachabilityStatus.OnUnselect(); // We show our own status.
    }
  }

  /// <inheritdoc/>
  public void OnUnselect() {
    _isSelected = false;
  }

  #endregion
}

}
