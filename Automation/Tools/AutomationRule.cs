// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Automation.Actions;
using Automation.Conditions;
using Automation.Core;
using Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.Persistence;
using Timberborn.PrefabSystem;

namespace Automation.Tools {

/// <summary>The base item of the automation.</summary>
/// <remarks>
/// Each rules defines and executes a term: "if &lt;condition> is true, then execute this &lt;action>"
/// </remarks>
public sealed class AutomationRule : IGameSerializable {
  #region Implementation of IGameSerializable
  static readonly PropertyKey<AutomationConditionBase> ConditionPropertyKey = new("Condition");
  static readonly PropertyKey<AutomationActionBase> ActionPropertyKey = new("Action");

  /// <inheritdoc/>
  public void LoadFrom(IObjectLoader objectLoader) {
    Condition = objectLoader.Get(ConditionPropertyKey, AutomationConditionBase.ConditionSerializer);
    Action = objectLoader.Get(ActionPropertyKey, AutomationActionBase.ActionSerializer);
    if (Action.Condition != null) {
      throw new InvalidOperationException("Rule spec must not have conditions: " + Action);
    }
  }

  /// <inheritdoc/>
  public void SaveTo(IObjectSaver objectSaver) {
    objectSaver.Set(ConditionPropertyKey, Condition, AutomationConditionBase.ConditionSerializer);
    objectSaver.Set(ActionPropertyKey, Action, AutomationActionBase.ActionSerializer);
  }
  #endregion

  #region API
  /// <summary>Serializer that handles persistence the rules.</summary>
  public static readonly StaticClassSerializer<AutomationRule> RuleSerializer = new();
  public AutomationConditionBase Condition { get; private set; }
  public AutomationActionBase Action { get; private set; }

  /// <summary>Needed for the persistence.</summary>
  public AutomationRule() {}

  /// <summary>Creates a rule that takes ownership on the provided condition and action.</summary>
  public AutomationRule(AutomationConditionBase condition, AutomationActionBase action) {
    Condition = condition;
    Action = action;
  }

  /// <summary>Verifies if the rule makes sense on the provided automation behavior.</summary>
  /// <remarks>This check must not result in any state changes of the rule, condition or action.</remarks>
  /// <param name="obj">The automation behavior candidate.</param>
  public bool IsValidAt(AutomationBehavior obj) {
    return Condition.IsValidAt(obj) && Action.IsValidAt(obj);
  }
  #endregion

  #region Implentation
  /// <inheritdoc/>
  public override string ToString() {
    var prefabName = Action.Behavior?.GetComponentFast<Prefab>()?.Name;
    var coords = Action.Behavior?.GetComponentFast<BlockObject>().Coordinates;
    return $"[Rule:condition=[{Condition}];action=[{Action}];at={prefabName}@{coords}]";
  }
  #endregion
}

}
