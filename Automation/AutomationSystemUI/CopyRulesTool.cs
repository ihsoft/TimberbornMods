// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngineUI;
using IgorZ.Automation.TemplateTools;
using IgorZ.TimberDev.Tools;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;
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
  protected override string CursorName => "AutomationCogCursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
    if (!behavior || behavior == _sourceRulesHelper.AutomationBehavior) {
      //FIXME
      DebugEx.Warning("CopyRulesTool: skip behavior {0}, object {1}", behavior, blockObject);
      return false;
    }
    return _actionsToCopy.All(x => x.Condition.IsValidAt(behavior) && x.CloneDefinition().IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponentFast<AutomationBehavior>();
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
  public override string WarningText() {
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
    _sourceRulesHelper = rulesHelper;
    _copyMode = copyMode;
    _actionsToCopy = copyMode switch {
        CopyMode.CopySignals => _sourceRulesHelper.BuildingSignals.Where(r => r.ExportedSignalName != null)
            .Select(x => x.Action)
            .ToList(),
        CopyMode.CopyRules => rulesHelper.BuildingRules,
        _ => throw new ArgumentException("Unknown copy mode"),
    };
    _toolGroupManager.CloseToolGroup();
    _toolManager.SwitchTool(this);
  }

  #endregion

  #region Implementation

  ToolManager _toolManager;
  RulesUIHelper _sourceRulesHelper;
  RulesUIHelper _targetRulesHelper;
  CopyMode _copyMode;
  IReadOnlyList<IAutomationAction> _actionsToCopy = [];

  /// <summary>Injects the condition dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(
      ToolManager toolManager, ToolGroupManager toolGroupManager, RulesUIHelper rulesHelper) {
    _toolManager = toolManager;
    _toolGroupManager = toolGroupManager;
    _targetRulesHelper = rulesHelper;
  }

  ToolGroupManager _toolGroupManager;

  #endregion
}
