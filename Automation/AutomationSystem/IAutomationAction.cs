// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.Utils;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>Base action interface.</summary>
/// <remarks>It defines what is expected from a regular action in the scope of the automation system.</remarks>
public interface IAutomationAction : IGameSerializable {
  /// <summary>Automation behavior this action belongs to.</summary>
  /// <remarks>
  /// <p>
  /// The action logic can detect when it is being activated or deactivated by checking the newly assigned value. The
  /// <c>null</c> value in this property means the action is inactive and mustn't be handling any logic. If a
  /// behavior is attached, then the action becomes active, and the behavior is "owning" it.
  /// </p>
  /// <p>If the behavior is being destroyed, it must unassign itself from all the owned action.</p>
  /// </remarks>
  /// <value><c>null</c> on the inactive listener.</value>
  public AutomationBehavior Behavior { get; set; }

  /// <summary>The condition which triggers this action.</summary>
  public IAutomationCondition Condition { get; set; }

  /// <summary>Indicates that the action had an error when attempted to be executed last time.</summary>
  /// <remarks>
  /// The error state means, the action fails and doesn't do its job. This should be considered a temporary state, and
  /// the action should be able to recover from it eventually.
  /// </remarks>
  public bool IsInErrorState { get; }

  /// <summary>Indicates that the action is not anymore needed and should be deleted.</summary>
  /// <remarks>Such actions are considered inactive and shouldn't process any logic.</remarks>
  public bool IsMarkedForCleanup { get; }

  /// <summary>Name of the template family that created this action.</summary>
  /// <remarks>
  /// Several templates can set the same automation, but with different condition settings. Such templates form
  /// "a family". Applying another template from the same family will clear the exiting automations from the same
  /// family. It is the template handler responsibility to deal with the existing automation.
  /// </remarks>
  public string TemplateFamily { get; set; }

  /// <summary>Returns a localized string to present the action description in UI.</summary>
  /// <remarks>
  /// The string must give an exhaustive description on what the action does, but at the same time it should be as short
  /// as possible. This property mustn't be accessed on an inactive action.
  /// </remarks>
  /// <seealso cref="Behavior"/>
  /// <seealso cref="AutomationBehavior.Loc"/>
  public string UiDescription { get; }

  /// <summary>Returns a full copy of the action <i>definition</i>. There must be no state copied.</summary>
  /// <remarks>This method MUST copy all the base class properties!</remarks>
  /// <seealso cref="TemplateFamily"/>
  public IAutomationAction CloneDefinition();

  /// <summary>Marks the condition for cleanup.</summary>
  public void MarkForCleanup();

  /// <summary>Verifies that the definitions of the two actions are equal.</summary>
  public bool CheckSameDefinition(IAutomationAction other);

  /// <summary>Verifies that the action can be used on the provided automation behavior.</summary>
  public bool IsValidAt(AutomationBehavior behavior);
}