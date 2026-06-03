// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.TimberDev.Settings;
using IgorZ.XRay.Core;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.XRay.Settings;

sealed class ColorSettings : BaseSettings<ColorSettings> {

  const string ColorCliffEdgeLocKey = "IgorZ.XRay.ColorSettings.Color.CliffEdge";
  const string ColorCliffLocKey = "IgorZ.XRay.ColorSettings.Color.Cliff";
  const string ColorGrassLocKey = "IgorZ.XRay.ColorSettings.Color.Grass";
  const string ColorWireframeEdgeLocKey = "IgorZ.XRay.ColorSettings.Color.WireframeEdge";
  const string ColorSchemaDropdownLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaDropdown";
  const string ColorSchemaNameBlueprintLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.Blueprint";
  const string ColorSchemaNameBwBrightLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.BWBright";
  const string ColorSchemaNameBwDarkLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.BWDark";
  const string ColorSchemaNameCustomLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.Custom";
  const string ColorSchemaNameNormalGlowLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.NormalGlow";
  const string ColorSchemaNameNormalLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.Normal";
  const string GhostModeIntensityLocKey = "IgorZ.XRay.ColorSettings.GhostModelIntensity";
  const string GlowingLocKey = "IgorZ.XRay.ColorSettings.Glowing";
  const string HeaderStringLocKey = "IgorZ.XRay.ColorSettings.Header";
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

  static readonly Preset[] SchemaPresets = [
      CustomSettings,
      BlueprintSchema,
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
      new(true, ModSettingDescriptor.CreateLocalized(GlowingLocKey).SetEnableCondition(IsCustom));

  public LimitedStringModSetting WireframeModeInternal { get; } =
    new(0,
        [
            new LimitedStringModSettingValue(nameof(WireframeTerrainMeshService.Mode.None), WireframeModeNoneLocKey),
            new LimitedStringModSettingValue(nameof(WireframeTerrainMeshService.Mode.Grid), WireframeModeGridLocKey),
            new LimitedStringModSettingValue(nameof(WireframeTerrainMeshService.Mode.Contours), WireframeModeContoursLocKey),
        ],
        ModSettingDescriptor.CreateLocalized(WireframeModeDropdownLocKey)
            .SetLocalizedTooltip(WireframeModeDropdownNoteLocKey)
            .SetEnableCondition(IsCustom));

  public ColorModSetting WireframeEdgeColor { get; } =
    new(DefaultSchema.WireframeEdgeColor,
        ModSettingDescriptor.CreateLocalized(ColorWireframeEdgeLocKey).SetEnableCondition(IsCustom),
        true);

  #endregion

  #region Implementation

  static ColorSettings _instance;

  ColorSettings(
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
