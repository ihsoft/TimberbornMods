// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

extern alias CustomTools;

using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockSystemNavigation;
using Timberborn.Buildings;
using Timberborn.BuildingsNavigation;
using Timberborn.ConstructionMode;
using Timberborn.Coordinates;
using Timberborn.PathSystem;
using UnityDev.Utils.LogUtilsLite;
using AbstractAreaSelectionTool = CustomTools::IgorZ.CustomTools.Tools.AbstractAreaSelectionTool;

namespace IgorZ.Automation.CommonTools;

/// <summary>Debug tool to show various information about block objects.</summary>
// ReSharper disable once ClassNeverInstantiated.Global
public class DebugPickTool : AbstractAreaSelectionTool, IConstructionModeEnabler {

  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = [
        Loc.T("IgorZ.Automation.DebugPickTool.DescriptionHint1"),
        Loc.T("IgorZ.Automation.DebugPickTool.DescriptionHint2"),
    ];
    DescriptionHintSection = null;
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

    var blockable = component.GetComponent<BlockableObject>();
    if (blockable && !blockable.IsUnblocked) {
      lines.AppendLine($"Building is blocked by:");
      var blockers = blockable._blockers.Select(x => x.ToString()).OrderBy(x => x);
      lines.AppendLine(string.Join("\n", blockers));
    }
    
    DebugEx.Warning(lines.ToString());
  }

  static void PrintAccessible(BaseComponent component) {
    var buildingAccessible = component.GetComponent<BuildingAccessible>()?.Accessible;
    var siteAccessible = component.GetComponent<ConstructionSiteAccessible>()?.Accessible;
    if (!buildingAccessible && !siteAccessible) {
      HostedDebugLog.Error(component, "No accessible components found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Accesses on {DebugEx.BaseComponentToString(component)}");
    if (buildingAccessible) {
      lines.AppendLine($"From BuildingAccessible (enabled={buildingAccessible.Enabled}):");
      if (!buildingAccessible.Accesses.IsEmpty()) {
        foreach (var access in buildingAccessible.Accesses) {
          lines.AppendLine($"- world: {access}, grid:{CoordinateSystem.WorldToGridInt(access)}");
        }
      } else {
        lines.AppendLine("- no entries");
      }
    }
    if (siteAccessible) {
      lines.AppendLine($"From ConstructionSiteAccessible (enabled={siteAccessible.Enabled}):");
      if (!siteAccessible.Accesses.IsEmpty()) {
        foreach (var access in siteAccessible.Accesses) {
          lines.AppendLine($"world: {access}, grid:{CoordinateSystem.WorldToGridInt(access)}");
        }
      } else {
        lines.AppendLine("- no entries");
      }
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }

  static void PrintNavMesh(BaseComponent component) {
    var settings = component.GetComponent<BlockObjectNavMeshSettingsSpec>();
    if (settings == null) {
      HostedDebugLog.Error(component, "No BlockObjectNavMeshSettings component found");
      return;
    }
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"NavMesh edges on {DebugEx.BaseComponentToString(component)}:");
    var isPath = component.GetComponent<PathSpec>() != null;
    var blockObject = component.GetComponent<BlockObject>();
    var isSolid = blockObject.Solid;
    lines.AppendLine($"Building: isPath={isPath}, isSolid={isSolid}");
    if (isSolid) {
      var blocks = blockObject.PositionedBlocks.GetAllBlocks();
      lines.AppendLine($"{blocks.Length} blocks:");
      foreach (var block in blocks) {
        lines.AppendLine($"{block.Coordinates}, stackable={block.Stackable}");
      }
    }
    var edges = settings.EdgeGroups.SelectMany(x => x.AddedEdges).ToList();
    lines.AppendLine($"{edges.Count} added edges:");
    foreach (var edge in edges) {
      lines.AppendLine($"{edge.Start} => {edge.End}");
    }
    lines.AppendLine($"{settings.UnblockedCoordinates.Length} unblocked coordinates:");
    foreach (var coords in settings.UnblockedCoordinates) {
      lines.AppendLine($"Group={coords.Group}, coords={coords.Coordinates}");
    }
    lines.AppendLine(new string('*', 10));
    DebugEx.Warning(lines.ToString());
  }
}