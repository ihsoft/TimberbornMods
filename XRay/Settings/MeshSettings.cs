// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.Settings;
using IgorZ.XRay.Core;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Common;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.XRay.Settings;

sealed class MeshSettings : BaseSettings<MeshSettings> {

  const string ColorCliffEdgeLocKey = "IgorZ.XRay.MeshSettings.Color.CliffEdge";
  const string ColorCliffLocKey = "IgorZ.XRay.MeshSettings.Color.Cliff";
  const string ColorGrassLocKey = "IgorZ.XRay.MeshSettings.Color.Grass";
  const string ColorWireframeEdgeLocKey = "IgorZ.XRay.MeshSettings.Color.WireframeEdge";
  const string ColorSchemaDropdownLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaDropdown";
  const string ColorSchemaNameBlueprintLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.Blueprint";
  const string ColorSchemaNameBlueGridLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.BlueGrid";
  const string ColorSchemaNameBwBrightLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.BWBright";
  const string ColorSchemaNameBwDarkLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.BWDark";
  const string ColorSchemaNameCustomLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.Custom";
  const string ColorSchemaNameNormalGlowLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.NormalGlow";
  const string ColorSchemaNameNormalLocKey = "IgorZ.XRay.MeshSettings.ColorSchemaName.Normal";
  const string GhostModeIntensityLocKey = "IgorZ.XRay.MeshSettings.GhostModelIntensity";
  const string GlowingLocKey = "IgorZ.XRay.MeshSettings.Glowing";
  const string HeaderStringLocKey = "IgorZ.XRay.MeshSettings.Header";
  const string WireframeModeDropdownLocKey = "IgorZ.XRay.MeshSettings.WireframeModeDropdown";
  const string WireframeModeDropdownNoteLocKey = "IgorZ.XRay.MeshSettings.WireframeModeDropdown.Note";
  const string WireframeModeNoneLocKey = "IgorZ.XRay.MeshSettings.WireframeMode.None";
  const string WireframeModeGridLocKey = "IgorZ.XRay.MeshSettings.WireframeMode.Grid";
  const string WireframeModeContoursLocKey = "IgorZ.XRay.MeshSettings.WireframeMode.Contours";

  record struct Preset(
      string NameLocKey, Color GrassColor, Color CliffColor, Color CliffEdgeColor,
      bool GlowingEdges, int GhostModeIntensity, string WireframeMode, Color WireframeEdgeColor);

  static readonly Preset CustomSettings =
      new(ColorSchemaNameCustomLocKey, HexColor(0), HexColor(0), HexColor(0), false, 0, "", HexColor(0));

  static readonly Preset BlueprintSchema =
      new(ColorSchemaNameBlueprintLocKey, HexColor(0x0036DA), HexColor(0x0036DA), HexColor(0x0036DA), false, 30,
          nameof(WireframeTerrainMeshService.Mode.Contours), HexColor(0xB2FFFFFF));
  static readonly Preset BlueGridSchema =
      new(ColorSchemaNameBlueGridLocKey, HexColor(0x0036DA), HexColor(0x0036DA), HexColor(0x0036DA), false, 30,
          nameof(WireframeTerrainMeshService.Mode.Grid), HexColor(0x18FFFFFF));
  static readonly Preset BwBrightSchema =
      new(ColorSchemaNameBwBrightLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), false, 21,
          nameof(WireframeTerrainMeshService.Mode.None), HexColor(0xCACACA));
  static readonly Preset BwDarkSchema =
      new(ColorSchemaNameBwDarkLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), false, 10,
          nameof(WireframeTerrainMeshService.Mode.None), HexColor(0xCACACA));
  static readonly Preset NormalSchema =
      new(ColorSchemaNameNormalLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), false, 10,
          nameof(WireframeTerrainMeshService.Mode.None), HexColor(0x00B196));
  static readonly Preset NormalGlowSchema =
      new(ColorSchemaNameNormalGlowLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), true, 10,
          nameof(WireframeTerrainMeshService.Mode.None), HexColor(0x00B196));

  static readonly LimitedStringModSettingValue[] WireframeModeValues = [
      new(nameof(WireframeTerrainMeshService.Mode.None), WireframeModeNoneLocKey),
      new(nameof(WireframeTerrainMeshService.Mode.Grid), WireframeModeGridLocKey),
      new(nameof(WireframeTerrainMeshService.Mode.Contours), WireframeModeContoursLocKey),
  ];
  static readonly string[] WireframeModeOptions = WireframeModeValues.Select(v => v.Value).ToArray();

  static readonly Preset[] SchemaPresets = [
      CustomSettings,
      BlueprintSchema,
      BlueGridSchema,
      BwBrightSchema,
      BwDarkSchema,
      NormalSchema,
      NormalGlowSchema,
  ];
  static readonly Preset DefaultSchema = BlueprintSchema;
  static readonly int DefaultSchemaIndex = SchemaPresets.ToList().IndexOf(DefaultSchema) != -1
    ? SchemaPresets.ToList().IndexOf(DefaultSchema)
    : throw new InvalidOperationException("Default schema is not found in presets");

  protected override string ModId => Configurator.AutomationModId;

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 0;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Settings

  public LimitedStringModSetting ColorSchemaInternal { get; } =
    new(
        DefaultSchemaIndex,
        SchemaPresets.Select(p => new LimitedStringModSettingValue(p.NameLocKey, p.NameLocKey)).ToArray(),
        ModSettingDescriptor.CreateLocalized(ColorSchemaDropdownLocKey));

  public ColorModSetting GrassColor { get; } =
      new(DefaultSchema.GrassColor,
          ModSettingDescriptor.CreateLocalized(ColorGrassLocKey).SetEnableCondition(IsCustom),
          false);

  public ColorModSetting CliffColor { get; } =
      new(DefaultSchema.CliffColor,
          ModSettingDescriptor.CreateLocalized(ColorCliffLocKey).SetEnableCondition(IsCustom),
          false);

  public ColorModSetting CliffEdgeColor { get; } =
      new(DefaultSchema.CliffEdgeColor,
          ModSettingDescriptor.CreateLocalized(ColorCliffEdgeLocKey).SetEnableCondition(IsCustom),
          false);

  public ModSetting<int> GhostModeIntensity { get; } =
      new RangeIntModSetting(
          DefaultSchema.GhostModeIntensity, 0, 100,
          ModSettingDescriptor.CreateLocalized(GhostModeIntensityLocKey).SetEnableCondition(IsCustom));

  public ModSetting<bool> GlowingEdges { get; } =
      new(DefaultSchema.GlowingEdges, ModSettingDescriptor.CreateLocalized(GlowingLocKey).SetEnableCondition(IsCustom));

  public LimitedStringModSetting WireframeModeInternal { get; } =
    new(((ICollection<string>)WireframeModeOptions).IndexOf(DefaultSchema.WireframeMode),
        WireframeModeValues,
        ModSettingDescriptor.CreateLocalized(WireframeModeDropdownLocKey)
            .SetLocalizedTooltip(WireframeModeDropdownNoteLocKey)
            .SetEnableCondition(IsCustom));

  public ColorModSetting WireframeEdgeColor { get; } =
    new(DefaultSchema.WireframeEdgeColor,
        ModSettingDescriptor.CreateLocalized(ColorWireframeEdgeLocKey).SetEnableCondition(IsCustom),
        true);

  #endregion

  #region Implementation

  static MeshSettings _instance;

  MeshSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
    InstallSettingCallback(ColorSchemaInternal, ApplyColorSchema);
  }

  void ApplyColorSchema() {
    DebugEx.Info("Apply schema: {0}", ColorSchemaInternal.Value);
    var preset = SchemaPresets.FirstOrDefault(x => x.NameLocKey == ColorSchemaInternal.Value);
    if (preset == default) {
      throw new InvalidOperationException($"Cannot find preset for schema: {ColorSchemaInternal.Value}");
    }
    if (preset.NameLocKey == ColorSchemaNameCustomLocKey) {
      return;
    }
    GrassColor.SetValue(preset.GrassColor);
    CliffColor.SetValue(preset.CliffColor);
    CliffEdgeColor.SetValue(preset.CliffEdgeColor);
    GlowingEdges.SetValue(preset.GlowingEdges);
    GhostModeIntensity.SetValue(preset.GhostModeIntensity);
    WireframeEdgeColor.SetValue(preset.WireframeEdgeColor);
    WireframeModeInternal.SetValue(preset.WireframeMode);
  }

  static bool IsCustom() => _instance.ColorSchemaInternal.Value == ColorSchemaNameCustomLocKey;

  static Color HexColor(uint colorIndex) {
    var alpha = colorIndex >> 24 & 0xFF;
    if (alpha == 0) {
      alpha = 0xFF;
    }
    return new Color(colorIndex >> 16 & 0xFF, colorIndex >> 8 & 0xFF, colorIndex & 0xFF, alpha) / 255f; 
  }

  #endregion
}
