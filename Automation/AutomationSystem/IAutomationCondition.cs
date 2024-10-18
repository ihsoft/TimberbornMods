// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.Utils;

namespace IgorZ.Automation.AutomationSystem {

/// <summary>Base condition interface.</summary>
/// <remarks>
/// The condition is a basic entity that does exactly two things: defines what to check; and setups a behavior component
/// that actually does the check during the game. The behavior component interacts with the condition, and the condition
/// notifies it's listener.
/// </remarks>
public interface IAutomationCondition : IGameSerializable {
  /// <summary>Automation behavior this condition belongs to.</summary>
  /// <remarks>
  /// <p>
  /// The condition logic can detect when it's being activated or deactivated by checking the newly assigned value. The
  /// <c>null</c> value in this property means the condition is inactive and must not be handling any logic. If a
  /// behavior is attached, then the condition becomes active, and the behavior is "owning" it.
  /// </p>
  /// <p>If the behavior is being destroyed, it must un-assign itself from all the owned conditions.</p>
  /// </remarks>
  /// <value><c>null</c> on the inactive condition.</value>
  public AutomationBehavior Behavior { get; set; }

  /// <summary>Listener that receives updates on the condition state changes.</summary>
  /// <remarks>
  /// The listener is responsible to decide on what to do next. It's basically "an action". The condition's role is only
  /// to notify that the state has updated, and the listener does the actual stuff.
  /// </remarks>
  /// <value><c>null</c> on the inactive condition.</value>
  /// <see cref="Behavior"/>
  public IAutomationConditionListener Listener { get; set; }

  /// <summary>The current condition state.</summary>
  /// <remarks>
  /// Value <c>true</c> is <c>ON</c> state, and <c>false</c> is <c>OFF</c>. The state change must be reported via
  /// <see cref="IAutomationConditionListener.OnConditionState"/>.
  /// </remarks>
  /// <seealso cref="Listener"/>
  public bool ConditionState { get; }

  /// <summary>Indicates that the condition is not anymore needed and should be deleted.</summary>
  /// <remarks>Such conditions are considered inactive and should not process any logic.</remarks>
  public bool IsMarkedForCleanup { get; }

  /// <summary>Returns a localized string to present the condition description in UI.</summary>
  /// <remarks>
  /// The string must give exhaustive description on what the condition checks, but at the same time it should be as
  /// short as possible. This property must not be accessed on an inactive condition.
  /// </remarks>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="AutomationBehavior.Loc"/>
  public string UiDescription { get; }

  /// <summary>Returns a full copy of the condition <i>definition</i>. There must be no state copied.</summary>
  public IAutomationCondition CloneDefinition();

  /// <summary>Verifies that the condition can be used on the provided automation behavior.</summary>
  public bool IsValidAt(AutomationBehavior behavior);

  /// <summary>
  /// Sets the current state of the condition so that it matches the current state of the game and/or the automation
  /// behavior.
  /// </summary>
  /// <remarks>
  /// This method must only be called if the condition is active. The state change, if any, must be reported as usual.
  /// </remarks>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="Listener"/>
  /// <seealso cref="ConditionState"/>
  public void SyncState();
}

}
