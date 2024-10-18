// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using System.Text;
using Automation.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsNavigation;
using Timberborn.Coordinates;
using Timberborn.Navigation;
using Timberborn.PathSystem;
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

  static void PrintAllComponents(BaseComponent component) {
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Components on {DebugEx.BaseComponentToString(component)}:");
    var names = component.AllComponents.Select(x => x.GetType().ToString()).OrderBy(x => x);
    lines.AppendLine(string.Join("\n", names));
    lines.AppendLine(new string('*', 10));

    var blockable = component.GetComponentFast<BlockableBuilding>();
    if (blockable && !blockable.IsUnblocked) {
      lines.AppendLine($"Building is blocked by:");
      var blockers = blockable._blockers.Select(x => x.ToString()).OrderBy(x => x);
      lines.AppendLine(string.Join("\n", blockers));
    }
    
    DebugEx.Warning(lines.ToString());
  }

  static void PrintAccessible(BaseComponent component) {
    var accessible = component.GetComponentFast<Accessible>();
    var siteAccessible = component.GetComponentFast<ConstructionSiteAccessible>()?.Accessible;
    if (!accessible && !siteAccessible) {
      HostedDebugLog.Error(component, "No accessible components found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Accesses on {DebugEx.BaseComponentToString(component)}");
    if (accessible) {
      lines.AppendLine($"From Accessible (enabled={accessible.enabled}):");
      foreach (var access in accessible.Accesses) {
        lines.AppendLine($"world: {access}, grid:{CoordinateSystem.WorldToGridInt(access)}");
      }
    }
    if (siteAccessible) {
      lines.AppendLine($"From ConstructionSiteAccessible (enabled={siteAccessible.enabled}):");
      foreach (var access in siteAccessible.Accesses) {
        lines.AppendLine($"world: {access}, grid:{CoordinateSystem.WorldToGridInt(access)}");
      }
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  static void PrintNavMesh(BaseComponent component) {
    var settings = component.GetComponentFast<BlockObjectNavMeshSettings>();
    if (!settings) {
      HostedDebugLog.Error(component, "No BlockObjectNavMeshSettings component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"NavMesh edges on {DebugEx.BaseComponentToString(component)}:");
    var isPath = (bool) component.GetComponentFast<Path>();
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
