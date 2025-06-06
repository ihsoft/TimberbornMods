// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Tools;
using Timberborn.BlockSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.ConstructionMode;
using UnityEngine;

namespace IgorZ.Automation.CommonTools;

// ReSharper disable once ClassNeverInstantiated.Global
sealed class PauseTool : AbstractLockingTool, IConstructionModeEnabler {

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(Color.red, Color.red, Color.white, Color.white);
    base.Initialize();
  }

  #endregion

  #region AbstractAreaSelectionTool overries

  /// <inheritdoc/>
  protected override string CursorName => "AutomationPauseCursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    if (!base.ObjectFilterExpression(blockObject)) {
      return false;
    }
    var component = GetCompatibleComponent(blockObject);
    return component && !component.Paused;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<PausableBuilding>().Pause();
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