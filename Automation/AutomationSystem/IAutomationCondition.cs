// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.Utils;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>Base condition interface.</summary>
/// <remarks>
/// The condition is a basic entity that does exactly two things: defines what to check; and sets up a behavior
/// component that actually does the check during the game. The behavior component interacts with the condition, and the
/// condition notifies its listener.
/// </remarks>
public interface IAutomationCondition : IGameSerializable {
  /// <summary>Automation behavior this condition belongs to.</summary>
  /// <remarks>
  /// <p>
  /// The condition logic can detect when it is being activated or deactivated by checking the newly assigned value. The
  /// <c>null</c> value in this property means the condition is inactive and mustn't be handling any logic. If a
  /// behavior is attached, then the condition becomes active, and the behavior is "owning" it.
  /// </p>
  /// <p>If the behavior is being destroyed, it must unassign itself from all the owned conditions.</p>
  /// <p>Once activated, the condition can't be reused, even if unassigned from the previous Behavior. Rather, it must be cloned to create a new instance.</p>
  /// </remarks>
  /// <value><c>null</c> on the inactive condition.</value>
  public AutomationBehavior Behavior { get; set; }

  /// <summary>Tells if the condition executes on a building, which construction is not yet finished.</summary>
  public bool CanRunOnUnfinishedBuildings { get; }

  /// <summary>Listener that receives updates on the condition state changes.</summary>
  /// <remarks>
  /// The listener is responsible to decide on what to do next. It is basically "an action". The condition's role is
  /// only to notify that the state has updated, and the listener does the actual stuff.
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
  /// <remarks>Such conditions are considered inactive and shouldn't process any logic.</remarks>
  public bool IsMarkedForCleanup { get; }

  /// <summary>Returns a localized string to present the condition description in UI.</summary>
  /// <remarks>
  /// The string must give an exhaustive description on what the condition checks, but at the same time it should be as
  /// short as possible. This property mustn't be accessed in an inactive condition.
  /// </remarks>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="AutomationBehavior.Loc"/>
  public string UiDescription { get; }

  /// <summary>Indicates that the condition is active and is processing state tracking logic.</summary>
  /// <seealso cref="Activate"/>
  public bool IsActive { get; }

  /// <summary>Returns a full copy of the condition <i>definition</i>. There must be no state copied.</summary>
  public IAutomationCondition CloneDefinition();

  /// <summary>Marks the condition for cleanup.</summary>
  public void MarkForCleanup();

  /// <summary>Verifies that the condition can be used on the provided automation behavior.</summary>
  public bool IsValidAt(AutomationBehavior behavior);

  /// <summary>
  /// Marks the condition as active. In this state, the condition can process logic and report state changes.
  /// </summary>
  /// <remarks>In this call, the condition should figure out and set its "current" state. And if the state evaluates to
  /// "true", then the associated listener is called.
  /// <see cref="Behavior"/> and <see cref="Listener"/> must be set before calling this method. This method should be
  /// called only once in life-time of the condition.
  /// </remarks>
  /// <param name="noTrigger">
  /// Indicates that condition should activate, but don't fire the state change callback even if the state is "true".
  /// </param>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="Listener"/>
  /// <seealso cref="IsActive"/> 
  public void Activate(bool noTrigger = false);
}