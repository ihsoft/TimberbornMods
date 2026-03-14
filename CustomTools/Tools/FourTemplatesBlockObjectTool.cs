// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.ToolSystemUI;
using UnityEngine.UIElements;

namespace IgorZ.CustomTools.Tools;

sealed class FourTemplatesBlockObjectTool
    : AbstractMultiTemplateBlockObjectTool<FourTemplatesBlockObjectTool.ModeType> {

  const string ShiftDescriptionHintLocKey = "IgorZ.CustomTools.FourTemplatesBlockObjectTool.ShiftDescriptionHint";
  const string CtrlDescriptionHintLocKey = "IgorZ.CustomTools.FourTemplatesBlockObjectTool.CtrlDescriptionHint";
  const string AltDescriptionHintLocKey = "IgorZ.CustomTools.FourTemplatesBlockObjectTool.AltDescriptionHint";

  public enum ModeType {
    NoModifier,
    ShiftModifier,
    CtrlModifier,
    AltModifier,
  }

  /// <inheritdoc/>
  protected override bool NeedEntityHeader => true;

  FourTemplatesToolSpec _fourTemplatesToolSpec;

  /// <inheritdoc/>
  protected override void Initialize() {
    _fourTemplatesToolSpec = ToolSpec.GetSpec<FourTemplatesToolSpec>();
    if (_fourTemplatesToolSpec == null) {
      throw new Exception($"FourTemplatesToolSpec not found on: {ToolSpec.Id}");
    }
    var bullets = new List<string>();
    if (_fourTemplatesToolSpec.ShiftModifierTemplate != null) {
      bullets.Add(
          Loc.T(ShiftDescriptionHintLocKey, GetTemplateDisplayName(GetTemplateForMode(ModeType.ShiftModifier))));
    }
    if (_fourTemplatesToolSpec.CtrlModifierTemplate != null) {
      bullets.Add(Loc.T(CtrlDescriptionHintLocKey, GetTemplateDisplayName(GetTemplateForMode(ModeType.CtrlModifier))));
    }
    if (_fourTemplatesToolSpec.AltModifierTemplate != null) {
      bullets.Add(Loc.T(AltDescriptionHintLocKey, GetTemplateDisplayName(GetTemplateForMode(ModeType.AltModifier))));
    }
    DescriptionBullets = bullets.ToArray();
    base.Initialize();
    CurrentMode = default;
  }

  /// <inheritdoc/>
  protected override void OnModeUpdated() {
    var labeledEntitySpec = Template.GetSpec<LabeledEntitySpec>();
    DescriptionHeaderElement.Q<Image>("Icon").sprite = labeledEntitySpec.Icon.Asset;
  }

  /// <inheritdoc/>
  public override ToolDescription DescribeTool() {
    var res = base.DescribeTool();
    OnModeUpdated();
    return res;
  }

  /// <inheritdoc/>
  protected override PlaceableBlockObjectSpec GetTemplateForMode(ModeType mode) {
    var templateName = mode switch {
        ModeType.ShiftModifier => _fourTemplatesToolSpec.ShiftModifierTemplate,
        ModeType.AltModifier => _fourTemplatesToolSpec.AltModifierTemplate,
        ModeType.CtrlModifier => _fourTemplatesToolSpec.CtrlModifierTemplate,
        ModeType.NoModifier => _fourTemplatesToolSpec.NoModifierTemplate,
        _ => throw new InvalidOperationException($"Unknown mode {mode}"),
    };
    if (templateName is null) {
      throw new InvalidOperationException($"No template set for mode: {mode}");
    }
    return _fourTemplatesToolSpec.FactionNeutral ? GetTemplateNoFaction(templateName) : GetTemplate(templateName);
  }

  /// <inheritdoc/>
  protected override ModeType SelectMode() {
    if (IsShiftHeld && _fourTemplatesToolSpec.ShiftModifierTemplate != null) {
      return ModeType.ShiftModifier;
    }
    if (IsAltHeld && _fourTemplatesToolSpec.AltModifierTemplate != null) {
      return ModeType.AltModifier;
    }
    if (IsCtrlHeld && _fourTemplatesToolSpec.CtrlModifierTemplate != null) {
      return ModeType.CtrlModifier;
    }
    return ModeType.NoModifier;
  }
}
