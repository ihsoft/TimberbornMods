// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using System.Text;
using Automation.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.Buildings;
using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Tools {

/// <summary>Debug tool to show various information about block objects.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugPickTool : AbstractAreaSelectionTool {
  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = new[]
        { "IgorZ.Automation.DebugPickTool.DescriptionHint1", "IgorZ.Automation.DebugPickTool.DescriptionHint2" };
    DescriptionHintSectionLoc = null;
    base.Initialize();
  }

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    return !IsAltHeld || !SelectionModeActive || SelectedObjects[0] == blockObject;
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    if (IsShiftHeld) {
      PrintAccessible(blockObject);
    } else if (IsCtrlHeld) {
      PrintNavMesh(blockObject);
    } else {
      PrintAllComponents(blockObject);
    }
  }

  internal static void PrintAllComponents(BaseComponent component) {
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Components on {DebugEx.BaseComponentToString(component)}:");
    var names = component.AllComponents.Select(x => x.GetType().ToString()).OrderBy(x => x);
    lines.AppendLine(string.Join("\n", names));
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  internal static void PrintAccessible(BaseComponent component) {
    var accessible = component.GetComponentFast<Accessible>();
    if (accessible == null) {
      HostedDebugLog.Error(component, "No accessible component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Accesses on {DebugEx.BaseComponentToString(component)}:");
    foreach (var access in accessible.Accesses) {
      lines.AppendLine(access.ToString());
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  internal static void PrintNavMesh(BaseComponent component) {
    var settings = component.GetComponentFast<BlockObjectNavMeshSettings>();
    if (settings == null) {
      HostedDebugLog.Error(component, "No BlockObjectNavMeshSettings component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"NavMesh edges on {DebugEx.BaseComponentToString(component)}:");
    var isPath = component.GetComponentFast<Building>().Path;
    var blockObject = component.GetComponentFast<BlockObject>();
    var isSolid = blockObject.Solid;
    lines.AppendLine($"Building: isPath={isPath}, isSolid={isSolid}");
    if (isSolid) {
      var blocks = blockObject.PositionedBlocks.GetAllBlocks();
      lines.AppendLine($"{blocks.Length} blocks:");
      foreach (var block in blocks) {
        lines.AppendLine($"{block.Coordinates}, stackable={block.Stackable}");
      }
    }
    var edges = settings.ManuallySetEdges().ToList();
    lines.AppendLine($"{edges.Count} edges:");
    foreach (var edge in edges) {
      lines.AppendLine($"{edge.Start} => {edge.End}");
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }
}

}
