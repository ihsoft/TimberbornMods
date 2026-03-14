// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionMode;
using UnityEngine;

namespace IgorZ.CustomTools.Tools;

sealed class ResumeTool : AbstractLockingTool, IConstructionModeEnabler {

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.green, Color.green, Color.white, Color.white);
    base.Initialize();
  }

  #endregion

  #region AbstractAreaSelectionTool overries

  /// <inheritdoc/>
  protected override string CursorName => "CustomToolsPlayCursor";

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
    blockObject.GetComponent<PausableBuilding>().Resume();
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
