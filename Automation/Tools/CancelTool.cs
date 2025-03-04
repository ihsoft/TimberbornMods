// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using UnityEngine;

namespace IgorZ.Automation.Tools;

// ReSharper disable once ClassNeverInstantiated.Global
sealed class CancelTool : AbstractAreaSelectionTool, IAutomationModeEnabler, IConstructionModeEnabler {

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.red, Color.red, Color.white, Color.white);
    base.Initialize();
  }

  #endregion

  #region AbstractAreaSelectionTool overrides

  /// <inheritdoc/>
  protected override string CursorName => "CancelCursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var component = blockObject.GetEnabledComponent<AutomationBehavior>();
    return component && component.HasActions;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<AutomationBehavior>().ClearAllRules();
  }

  #endregion
}