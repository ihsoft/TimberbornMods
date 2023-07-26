// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Utils;

namespace Automation.Core {

/// <summary>Base action interface.</summary>
/// <remarks>
/// It defines what is expected from a regular action n scope of the automation system.
/// </remarks>
public interface IAutomationAction : IGameSerializable {
  /// <summary>Automation behavior this action belongs to.</summary>
  /// <remarks>
  /// <p>
  /// The action logic can detect when it's being activated or deactivated by checking the newly assigned value. The
  /// <c>null</c> value in this property means the action is inactive and must not be handling any logic. If a
  /// behavior is attached, then the action becomes active, and the behavior is "owning" it.
  /// </p>
  /// <p>If the behavior is being destroyed, it must un-assign itself from all the owned action.</p>
  /// </remarks>
  /// <value><c>null</c> on the inactive listener.</value>
  public AutomationBehavior Behavior { get; set; }

  /// <summary>The condition which triggers this action.</summary>
  public IAutomationCondition Condition { get; set; }

  /// <summary>Indicates that the action is not anymore needed and should be deleted.</summary>
  /// <remarks>Such actions are considered inactive and should not process any logic.</remarks>
  public bool IsMarkedForCleanup { get; }

  /// <summary>Returns a localized string to present the action description in UI.</summary>
  /// <remarks>
  /// The string must give exhaustive description on what the action does, but at the same time it should be as short as
  /// possible. This property must not be accessed on an inactive action.
  /// </remarks>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="AutomationBehavior.Loc"/>
  public string UiDescription { get; }

  /// <summary>Returns a full copy of the action <i>definition</i>. There must be no state copied.</summary>
  public IAutomationAction CloneDefinition();

  /// <summary>Verifies that the definitions of the two actions are equal.</summary>
  public bool CheckSameDefinition(IAutomationAction other);

  /// <summary>Verifies that the action can be used on the provided automation behavior.</summary>
  public bool IsValidAt(AutomationBehavior behavior);
}

}
