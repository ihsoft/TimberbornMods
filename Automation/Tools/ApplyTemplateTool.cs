// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Automation.Core;
using Automation.Tools;
using Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.Persistence;
using UnityEngine;

namespace Automation.Templates {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class ApplyTemplateTool : AbstractAreaSelectionTool, IAutomationModeEnabler {
  static readonly Color ToolColor = new(0, 1, 1, 0.7f);

  #region Tool information
  static readonly ListKey<AutomationRule> RulesListKey = new("Rules");
  public sealed class ToolInfo : CustomToolSystem.ToolInformation {
    public ReadOnlyCollection<AutomationRule> Rules { get; private set; }

    /// <inheritdoc/>
    public override void Load(IObjectLoader objectLoader) {
      Rules = objectLoader.Get(RulesListKey, AutomationRule.RuleSerializer).AsReadOnly();
    }
  }
  #endregion

  #region AbstractAreaSelectionTool overries
  /// <inheritdoc/>
  protected override string CursorName => "igorz.automation/cursors/cog-cursor-large";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    if (behavior == null || !behavior.enabled) {
      return false;
    }
    var info = (ToolInfo) ToolInformation;
    return info.Rules.All(rule => rule.IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    behavior.ClearActions();
    var info = (ToolInfo) ToolInformation;
    foreach (var rule in info.Rules) {
      if (rule.Action is not IAutomationConditionListener listener) {
        throw new InvalidOperationException("Action is not a condition state listener: " + rule.Action);
      }
      behavior.AddRule(rule.Condition.CloneDefinition(), rule.Action.CloneDefinition());
    }
  }

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(ToolColor, ToolColor, Color.cyan, Color.cyan);
    base.Initialize();
  }
  #endregion
}

}
