// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

extern alias CustomTools;

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.TemplateTools;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.ToolSystem;
using UnityEngine;
using AbstractAreaSelectionTool = CustomTools::IgorZ.CustomTools.Tools.AbstractAreaSelectionTool;

namespace IgorZ.Automation.AutomationSystemUI;

sealed class CopyRulesTool : AbstractAreaSelectionTool, IAutomationModeEnabler, IConstructionModeEnabler {

  const string DescriptionTitleLocKey = "IgorZ.Automation.Tool.CopyRules.Title";
  const string CopyRulesTextLocKey = "IgorZ.Automation.Tool.CopyRules.Text";

  static readonly Color HighlightColor = new(0, 1, 0, 0.7f);
  static readonly Color SelectionColor = Color.white;

  #region AbstractAreaSelectionTool overries

  /// <inheritdoc/>
  protected override string DescriptionTitle => Loc.T(DescriptionTitleLocKey);

  /// <inheritdoc/>
  protected override string DescriptionMainSection => null;

  /// <inheritdoc/>
  protected override string CursorName => "AutomationCogCursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponent<AutomationBehavior>();
    if (!behavior || behavior == _sourceBehavior) {
      return false;
    }
    return _actionsToCopy.All(x => x.Condition.IsValidAt(behavior) && x.CloneDefinition().IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponent<AutomationBehavior>();
    _targetRulesHelper.SetBuilding(behavior);
    if (_copyMode == CopyMode.CopyRules) {
      _targetRulesHelper.ClearRulesOnBuilding();
    } else {
      _targetRulesHelper.ClearSignalsOnBuilding();
    }
    foreach (var action in _actionsToCopy) {
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
  public override string GetWarningText() {
    return Loc.T(CopyRulesTextLocKey, _actionsToCopy.Count);
  }

  #endregion

  #region API

  public enum CopyMode {
    CopySignals,
    CopyRules,
  }

  public void StartTool(RulesUIHelper rulesHelper, CopyMode copyMode) {
    Initialize();
    _sourceBehavior = rulesHelper.AutomationBehavior;
    _copyMode = copyMode;
    _actionsToCopy = copyMode switch {
        CopyMode.CopySignals => rulesHelper.BuildingSignals.Where(r => r.ExportedSignalName != null)
            .Select(x => x.Action)
            .ToList(),
        CopyMode.CopyRules => rulesHelper.BuildingRules.ToList(),  // Need a copy.
    };
    _toolGroupService.ExitToolGroup();
    _toolService.SwitchTool(this);
  }

  #endregion

  #region Implementation

  ToolService _toolService;
  ToolGroupService _toolGroupService;
  AutomationBehavior _sourceBehavior;
  RulesUIHelper _targetRulesHelper;
  CopyMode _copyMode;
  IReadOnlyList<IAutomationAction> _actionsToCopy = [];

  /// <summary>Injects the condition dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(
      ToolService toolService, ToolGroupService toolGroupService, RulesUIHelper rulesHelper) {
    _toolService = toolService;
    _toolGroupService = toolGroupService;
    _targetRulesHelper = rulesHelper;
  }

  #endregion
}
