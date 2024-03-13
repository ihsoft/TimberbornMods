// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

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
  /// <summary>Class that holds the template tool configuration.</summary>
  public sealed class ToolInfo : CustomToolSystem.ToolInformation {
    static readonly ListKey<AutomationRule> RulesListKey = new(nameof(Rules));
    static readonly PropertyKey<string> TemplateFamilyNameKey = new(nameof(TemplateFamilyName));

    /// <summary>Full list of the rules that this template should apply on the object.</summary>
    public ReadOnlyCollection<AutomationRule> Rules { get; private set; }

    /// <summary>
    /// A logical rules group name. Rules from the same group will overwrite any existing rules from the same group.
    /// </summary>
    public string TemplateFamilyName { get; private set; }

    /// <inheritdoc/>
    public override void Load(IObjectLoader objectLoader) {
      TemplateFamilyName = objectLoader.Get(TemplateFamilyNameKey);
      Rules = objectLoader.Get(RulesListKey, AutomationRule.RuleSerializer).AsReadOnly();
    }
  }
  #endregion

  #region AbstractAreaSelectionTool overries
  /// <inheritdoc/>
  protected override string CursorName => "igorz.automation/cursors/cog-cursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    if (!behavior || !behavior.enabled) {
      return false;
    }
    var info = (ToolInfo) ToolInformation;
    return info.Rules.All(rule => rule.IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var info = (ToolInfo) ToolInformation;
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    behavior.RemoveRulesForTemplateFamily(info.TemplateFamilyName);
    foreach (var rule in info.Rules) {
      var action = rule.Action.CloneDefinition();
      action.TemplateFamily = info.TemplateFamilyName;
      behavior.AddRule(rule.Condition.CloneDefinition(), action);
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
