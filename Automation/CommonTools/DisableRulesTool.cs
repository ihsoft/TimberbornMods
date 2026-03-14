// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

extern alias CustomTools;
using System.Linq;
using CustomTools::IgorZ.CustomTools.Tools;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.TemplateTools;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using UnityEngine;

namespace IgorZ.Automation.CommonTools;

// ReSharper disable once ClassNeverInstantiated.Global
sealed class DisableRulesTool : AbstractAreaSelectionTool, IAutomationModeEnabler, IConstructionModeEnabler {

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.red, Color.red, Color.white, Color.white);
    base.Initialize();
  }

  #endregion

  #region AbstractAreaSelectionTool overrides

  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var component = blockObject.GetEnabledComponent<AutomationBehavior>();
    return component && component.HasActions;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponent<AutomationBehavior>();
    var rulesCopy = behavior.Actions.ToArray();
    behavior.ClearAllRules();
    foreach (var rule in rulesCopy) {
      var condition = rule.Condition.CloneDefinition();
      condition.SetEnabled(false);
      behavior.AddRule(condition, rule.CloneDefinition());
    }
  }

  #endregion
}
