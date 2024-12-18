// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using UnityEngine;

namespace IgorZ.Automation.Tools;

// ReSharper disable once ClassNeverInstantiated.Global
sealed class ResumeTool : AbstractLockingTool {
  #region CustomTool overrides
  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.green, Color.green, Color.white, Color.white);
    base.Initialize();
  }
  #endregion

  #region AbstractAreaSelectionTool overries
  /// <inheritdoc/>
  protected override string CursorName => "IgorZ/play-cursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    if (!base.ObjectFilterExpression(blockObject)) {
      return false;
    }
    var component = GetCompatibleComponent(blockObject);
    return component && component.Paused;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<PausableBuilding>().Resume();
  }
  #endregion

  #region AbstractLockingTool overries
  /// <inheritdoc/>
  protected override bool CheckCanLockOnComponent(BlockObject obj) {
    return GetCompatibleComponent(obj);
  }
  #endregion

  #region Implementation
  static PausableBuilding GetCompatibleComponent(BlockObject obj) {
    var component = obj.GetEnabledComponent<PausableBuilding>();
    if (component && component.IsPausable()) {
      return component;
    }
    return null;
  }
  #endregion
}