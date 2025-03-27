// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.TemplateTools;
using IgorZ.TimberDev.Tools;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.ToolSystem;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class CopyRulesTool : AbstractAreaSelectionTool, IAutomationModeEnabler, IConstructionModeEnabler {

  const string DescriptionTitleLocKey = "IgorZ.Automation.Tool.CopyRules.Title";
  const string CopyRulesTextLocKey = "IgorZ.Automation.Tool.CopyRules.Text";

  static readonly Color HighlightColor = new(0, 1, 0, 0.7f);
  static readonly Color SelectionColor = Color.white;

  #region AbstractAreaSelectionTool overries

  protected override string DescriptionTitleLoc => DescriptionTitleLocKey;

  /// <inheritdoc/>
  protected override string CursorName => "IgorZ/cog-cursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    if (!behavior || behavior == _copySource) {
      return false;
    }
    return _copySource.Actions.All(action => action.Condition.IsValidAt(behavior) && action.IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    behavior.ClearAllRules();
    foreach (var action in _copySource.Actions) {
      behavior.AddRule(action.Condition.CloneDefinition(), action.CloneDefinition());
    }
  }

  #endregion

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(HighlightColor, HighlightColor, SelectionColor, SelectionColor);
    base.Initialize();
  }

  /// <inheritdoc/>
  public override string WarningText() {
    return Loc.T(CopyRulesTextLocKey, _copySource.Actions.Count());
  }

  #endregion

  #region Implementation

  ToolManager _toolManager;
  AutomationBehavior _copySource;

  /// <summary>Injects the condition dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(ToolManager toolManager, ToolGroupManager toolGroupManager) {
    _toolManager = toolManager;
    _toolGroupManager = toolGroupManager;
  }

  ToolGroupManager _toolGroupManager;

  public void StartTool(AutomationBehavior behavior) {
    Initialize();
    _copySource = behavior;
    _toolGroupManager.CloseToolGroup();
    _toolManager.SwitchTool(this);
  }

  #endregion
}
