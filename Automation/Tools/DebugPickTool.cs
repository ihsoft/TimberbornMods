// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text;
using Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Tools {

/// <summary>Debug tool to show various information about block objects.</summary>
public class DebugPickTool : AbstractAreaSelectionTool {
  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = new[]
        { "IgorZ.Automation.DebugPickTool.DescriptionHint1", "IgorZ.Automation.DebugPickTool.DescriptionHint2" };
    base.Initialize();
  }

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    return true;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    if (InputService.IsShiftHeld) {
      PrintAccessible(blockObject);
    } else if (InputService.IsCtrlHeld) {
      PrintNavMesh(blockObject);
    } else {
      PrintAllComponents(blockObject);
    }
  }

  void PrintAllComponents(BlockObject blockObject) {
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Components on {DebugEx.BaseComponentToString(blockObject)}:");
    foreach (var comp in blockObject.AllComponents) {
      lines.AppendLine(comp.GetType().ToString());
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  void PrintAccessible(BlockObject blockObject) {
    var accessible = blockObject.GetComponentFast<Accessible>();
    if (accessible == null) {
      HostedDebugLog.Error(blockObject, "No accessible component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Accesses on {DebugEx.BaseComponentToString(blockObject)}:");
    foreach (var access in accessible.Accesses) {
      lines.AppendLine(access.ToString());
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  void PrintNavMesh(BlockObject blockObject) {
    var settings = blockObject.GetComponentFast<BlockObjectNavMeshSettings>();
    if (settings == null) {
      HostedDebugLog.Error(blockObject, "No BlockObjectNavMeshSettings component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"NavMesh edges on {DebugEx.BaseComponentToString(blockObject)}:");
    foreach (var edge in settings.ManuallySetEdges()) {
      lines.AppendLine($"{edge.Start} => {edge.End}");
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }
}

}
