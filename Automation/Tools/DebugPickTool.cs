// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text;
using Automation.Utils;
using Timberborn.BlockSystem;
using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Tools {

/// <summary>Debug tool to show various information about block objects.</summary>
public class DebugPickTool : AbstractAreaSelectionTool {
  /// <inheritdoc/>
  protected override string CursorName => null;

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = new[] { "IgorZ.Automation.DebugPickTool.DescriptionHint" };
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
    } else {
      PrintAllComponents(blockObject);
    }
  }

  void PrintAllComponents(BlockObject blockObject) {
    var names = new StringBuilder();
    names.AppendLine(new string('*', 10));
    names.AppendLine("Components on " + DebugEx.BaseComponentToString(blockObject) + ":");
    foreach (var comp in blockObject.AllComponents) {
      names.AppendLine(comp.GetType().ToString());
    }
    names.AppendLine(new string('*', 10));
    DebugEx.Warning(names.ToString());
  }

  void PrintAccessible(BlockObject blockObject) {
    var accessible = blockObject.GetComponentFast<Accessible>();
    if (accessible == null) {
      HostedDebugLog.Error(blockObject, "No accessible component found");
      return;
    }
    var names = new StringBuilder();
    names.AppendLine(new string('*', 10));
    names.AppendLine("Accesses on " + DebugEx.BaseComponentToString(blockObject) + ":");
    foreach (var comp in accessible.Accesses) {
      names.AppendLine(comp.ToString());
    }
    names.AppendLine(new string('*', 10));
    DebugEx.Warning(names.ToString());
  }
}

}
