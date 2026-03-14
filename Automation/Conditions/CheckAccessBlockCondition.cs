// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.PathCheckingSystem;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BuildingsNavigation;
using Timberborn.Persistence;

namespace IgorZ.Automation.Conditions;

/// <summary>
/// This condition verifies if construction completion of the object prevents accessing the other objects being
/// constructed.
/// </summary>
/// <remarks>This logic works only on the set of the objects that has this condition assigned to. It won't check all the
/// objects being constructed in the scene.
/// </remarks>
sealed class CheckAccessBlockCondition : AutomationConditionBase {
  const string BlockingPathNameLocKey = "IgorZ.Automation.CheckAccessBlockCondition.Blocking.Description";
  const string NotBlockingPathNameLocKey = "IgorZ.Automation.CheckAccessBlockCondition.NotBlocking.Description"; 

  /// <summary>
  /// Indicates that the action should be triggered on an inversed condition. That is: the object will not block the
  /// access on completion.
  /// </summary>
  public bool IsReversedCondition { get; private set; }

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override bool CanRunOnUnfinishedBuildings => true;

  /// <inheritdoc/>
  public override string UiDescription =>
      CommonFormats.HighlightYellow(
          Behavior.Loc.T(!IsReversedCondition ? BlockingPathNameLocKey : NotBlockingPathNameLocKey));

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return !behavior.BlockObject.IsFinished && behavior.GetComponent<ConstructionSiteAccessible>();
  }

  /// <inheritdoc/>
  public override bool IsInErrorState => false; // This condition doesn't have any error state.

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    var clone = (CheckAccessBlockCondition)base.CloneDefinition();
    clone.IsReversedCondition = IsReversedCondition;
    return clone;
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    if (IsEnabled) {
      StaticBindings.DependencyContainer.GetInstance<PathCheckingService>().AddCondition(this);
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    if (IsEnabled) {
      StaticBindings.DependencyContainer.GetInstance<PathCheckingService>().RemoveCondition(this);
    }
  }

  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<bool> IsReversedConditionKey = new("ReversedCondition");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    IsReversedCondition = objectLoader.GetValueOrDefault(IsReversedConditionKey);
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
    MarkForCleanup();
  }

  #endregion
}