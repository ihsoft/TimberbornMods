// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.AutomationSystem;
using Automation.PathCheckingSystem;
using TimberApi.DependencyContainerSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Persistence;

namespace Automation.Conditions {

/// <summary>
/// This condition verifies if construction completion of the object would prevent accessing the other objects being
/// constructed.
/// </summary>
/// <remarks>This logic works only on the set of the objects that has this condition assigned to. It won't check all the
/// objects being constructed in the scene.
/// </remarks>
public sealed class CheckAccessBlockCondition : AutomationConditionBase {
  const string BlockingPathNameLocKey = "IgorZ.Automation.CheckAccessBlockCondition.Blocking.Description";
  const string NotBlockingPathNameLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotBlocking.Description"; 

  /// <summary>
  /// Indicates that the the action should be triggered on an inversed condition. That is: the object will not block the
  /// access on completion.
  /// </summary>
  public bool IsReversedCondition { get; private set; }

  #region AutomationConditionBase implementation
  /// <inheritdoc/>
  public override string UiDescription =>
      Behavior.Loc.T(!IsReversedCondition ? BlockingPathNameLocKey : NotBlockingPathNameLocKey);

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return !behavior.BlockObject.IsFinished && behavior.GetComponentFast<ConstructionSiteAccessible>();
  }

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new CheckAccessBlockCondition { IsReversedCondition = IsReversedCondition };
  }

  /// <inheritdoc/>
  public override void SyncState() {
    // Game tick required.
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    DependencyContainer.GetInstance<PathCheckingService>().AddCondition(this);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    DependencyContainer.GetInstance<PathCheckingService>().RemoveCondition(this);
  }
  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<bool> IsReversedConditionKey = new("ReversedCondition");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    IsReversedCondition = objectLoader.GetValueOrNullable(IsReversedConditionKey) ?? false;
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(IsReversedConditionKey, IsReversedCondition);
  }
  #endregion

  #region Implementation
  /// <summary>Removes the condition from the object, but it must only be called on an active condition.</summary>
  internal void CancelCondition() {
    IsMarkedForCleanup = true;
    Behavior.CollectCleanedRules();
  }
  #endregion
}

}
