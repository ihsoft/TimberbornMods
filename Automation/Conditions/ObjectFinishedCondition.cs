// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.ConstructibleSystem;
using Timberborn.SingletonSystem;

namespace Automation.Conditions {

/// <summary>Condition that triggers when the object enters the finished state.</summary>
/// <remarks>This condition must be used with caution. Any finished building gets this event during the load.</remarks>
public sealed class ObjectFinishedCondition : AutomationConditionBase {
  const string DescriptionLocKey = "IgorZ.Automation.ObjectFinishedCondition.Description";

  #region BlockObjectConditionBase implementation
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new ObjectFinishedCondition();
  }

  /// <inheritdoc/>
  public override void SyncState() {
    ConditionState = Behavior.BlockObject.Finished;
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return true;  // On the finished objects the condition will trigger immediately.
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    Behavior.EventBus.Register(this);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Behavior.EventBus.Unregister(this);
  }
  #endregion

  #region Implementation
  /// <summary>Triggers when the object or building becomes a "constructed" entity.</summary>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    if (@event.Constructible.GameObjectFast != Behavior.GameObjectFast) {
      return;
    }
    ConditionState = true;
    IsMarkedForCleanup = true;
    //FIXME: call system to have the state accounted.
  }
  #endregion
}

}
