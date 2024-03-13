// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Timberborn.Persistence;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace Automation.Actions {

/// <summary>Adjusts flood gate height.</summary>
// ReSharper disable once UnusedType.Global
public sealed class SetFloodgateHeightAction : AutomationActionBase {
  const string DescriptionLocKey = "IgorZ.Automation.SetFloodgateHeightAction.Description";

  /// <summary>Number of 0.5m steps down from the maximum floodgate height.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public int StepsDown { get; private set; }

  Floodgate Floodgate => Behavior.GetComponentFast<Floodgate>();
  float TargetHeight => Mathf.Max(0, Floodgate.MaxHeight - 0.5f * StepsDown);

  #region AutomationActionBase overrides
  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(DescriptionLocKey, TargetHeight);

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new SetFloodgateHeightAction { TemplateFamily = TemplateFamily, StepsDown = StepsDown };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    var component = behavior.GetComponentFast<Floodgate>();
    return component && component.enabled;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    Floodgate.SetHeightAndSynchronize(TargetHeight);
  }
  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<int> StepsDownPropertyKey = new("StepsDown");

  /// <summary>Loads action state and declaration.</summary>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    StepsDown = objectLoader.Get(StepsDownPropertyKey);
  }

  /// <summary>Saves action state and declaration.</summary>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(StepsDownPropertyKey, StepsDown);
  }
  #endregion
}

}
