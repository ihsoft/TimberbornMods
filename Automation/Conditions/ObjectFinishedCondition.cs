// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.ConstructibleSystem;
using Timberborn.SingletonSystem;

namespace Automation.Conditions {

public sealed class ObjectFinishedCondition : AutomationConditionBase {
  #region BlockObjectConditionBase implementation
  /// <inheritdoc/>
  public override string UiDescription => "<SolidHighlight>construction complete</SolidHighlight>";

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
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent @event) {
    if (@event.Constructible.GameObjectFast != Behavior.GameObjectFast) {
      return;
    }
    ConditionState = true;
    IsMarkedForCleanup = true;
  }
  #endregion
}

}
