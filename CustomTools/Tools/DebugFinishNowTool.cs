// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.ConstructionSites;

namespace IgorZ.CustomTools.Tools;

/// <summary>Debug tool to immediately complete unfinished constructibles.</summary>
sealed class DebugFinishNowTool : AbstractAreaSelectionTool, IConstructionModeEnabler {

  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var component = blockObject.GetComponent<ConstructionSite>();
    return component && !blockObject.IsFinished;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponent<ConstructionSite>().FinishNow();
  }
}