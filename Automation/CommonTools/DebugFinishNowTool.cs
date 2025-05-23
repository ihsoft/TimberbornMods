﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Tools;
using Timberborn.BlockSystem;
using Timberborn.ConstructionMode;
using Timberborn.ConstructionSites;

namespace IgorZ.Automation.CommonTools;

/// <summary>Debug tool to immediately complete unfinished constructibles.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugFinishNowTool : AbstractAreaSelectionTool, IConstructionModeEnabler {

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