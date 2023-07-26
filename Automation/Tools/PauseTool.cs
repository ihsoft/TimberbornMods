// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using UnityEngine;

namespace Automation.Tools {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class PauseTool : AbstractLockingTool {
  #region CustomTool overrides
  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.red, Color.red, Color.white, Color.white);
    base.Initialize();
  }
  #endregion

  #region AbstractAreaSelectionTool overries
  /// <inheritdoc/>
  protected override string CursorName => "igorz.automation/cursors/pause-cursor-large";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    if (!base.ObjectFilterExpression(blockObject)) {
      return false;
    }
    var component = GetCompatibleComponent(blockObject);
    return component != null && !component.Paused;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<PausableBuilding>().Pause();
  }
  #endregion

  #region AbstractLockingTool overries
  /// <inheritdoc/>
  protected override bool CheckCanLockOnComponent(BlockObject obj) {
    return GetCompatibleComponent(obj) != null;
  }
  #endregion

  #region Implementation
  static PausableBuilding GetCompatibleComponent(BlockObject obj) {
    var component = obj.GetComponentFast<PausableBuilding>();
    if (component != null && component.enabled && component.IsPausable()) {
      return component;
    }
    return null;
  }
  #endregion
}

}
