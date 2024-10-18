// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;

namespace IgorZ.Automation.Tools {

/// <summary>Debug tool to immediately complete unfinished constructibles.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugFinishNowTool : AbstractAreaSelectionTool {
  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var component = blockObject.GetComponentFast<ConstructionSite>();
    return component && !blockObject.IsFinished;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    blockObject.GetComponentFast<ConstructionSite>().FinishNow();
  }
}

}
