// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.TimberDev.Settings;
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
  const string ColorSchemaNameBwBrightLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.BWBright";
  const string ColorSchemaNameBwDarkLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.BWDark";
  const string ColorSchemaNameCustomLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.Custom";
  const string ColorSchemaNameNormalGlowLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.NormalGlow";
  const string ColorSchemaNameNormalLocKey = "IgorZ.XRay.ColorSettings.ColorSchemaName.Normal";
  const string GhostModeIntensityLocKey = "IgorZ.XRay.ColorSettings.GhostModelIntensity";
  const string GlowingLocKey = "IgorZ.XRay.ColorSettings.Glowing";
  const string HeaderStringLocKey = "IgorZ.XRay.ColorSettings.Header";

  record struct Preset(
      string NameLocKey, Color GrassColor, Color CliffColor, Color CliffEdgeColor, Color WireframeEdgeColor,
      bool GlowingEdges, int GhostModeIntensity);

  static readonly Preset DefaultSchema =
      new(ColorSchemaNameBwBrightLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), HexColor(0xCACACA), false, 21);
  static readonly Preset CustomSettings =
      new(ColorSchemaNameCustomLocKey, HexColor(0), HexColor(0), HexColor(0), HexColor(0), false, 0);

  static readonly Preset[] SchemaPresets = [
      CustomSettings,
      new(ColorSchemaNameNormalLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), HexColor(0x00B196), false, 10),
      new(ColorSchemaNameNormalGlowLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), HexColor(0x00B196), true, 10),
      new(ColorSchemaNameBwDarkLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), HexColor(0xCACACA), false, 10),
      DefaultSchema,
  ];
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

  public ColorModSetting WireframeEdgeColor { get; } =
    new(DefaultSchema.WireframeEdgeColor,
        ModSettingDescriptor.CreateLocalized(ColorWireframeEdgeLocKey).SetEnableCondition(IsCustom),
        false);

  public ModSetting<int> GhostModeIntensity { get; } =
      new RangeIntModSetting(
          DefaultSchema.GhostModeIntensity, 0, 100,
          ModSettingDescriptor.CreateLocalized(GhostModeIntensityLocKey).SetEnableCondition(IsCustom));

  public ModSetting<bool> GlowingEdges { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(GlowingLocKey).SetEnableCondition(IsCustom));

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
    WireframeEdgeColor.SetValue(preset.WireframeEdgeColor);
    GlowingEdges.SetValue(preset.GlowingEdges);
    GhostModeIntensity.SetValue(preset.GhostModeIntensity);
  }

  static bool IsCustom() => _instance.ColorSchemaInternal.Value == ColorSchemaNameCustomLocKey;

  static Color HexColor(int colorIndex) {
    return new Color(colorIndex >> 16 & 0xFF, colorIndex >> 8 & 0xFF, colorIndex & 0xFF) / 255f; 
  }

  #endregion
}
