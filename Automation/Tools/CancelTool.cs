// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Core;
using Automation.Utils;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Automation.Tools {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class CancelTool : AbstractAreaSelectionTool, IAutomationModeEnabler {
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
    var automationBehavior = blockObject.GetComponentFast<AutomationBehavior>();
    return automationBehavior != null && automationBehavior.enabled && automationBehavior.HasActions;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<AutomationBehavior>().ClearActions();
  }
  #endregion
}

}
