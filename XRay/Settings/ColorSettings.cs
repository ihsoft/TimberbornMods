// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.XRay.Settings;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
sealed class ColorSettings : BaseSettings<ColorSettings> {

  const string ColorCliffEdgeLocKey = "IgorZ.XRay.ColorSettings.Color.CliffEdge";
  const string ColorCliffLocKey = "IgorZ.XRay.ColorSettings.Color.Cliff";
  const string ColorGrassLocKey = "IgorZ.XRay.ColorSettings.Color.Grass";
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
      string NameLocKey, Color GrassColor, Color CliffColor, Color CliffEdgeColor, bool GlowingEdges,
      int GhostModeIntensity);

  static readonly Preset[] SchemaPresets = [
      new(ColorSchemaNameCustomLocKey, HexColor(0), HexColor(0), HexColor(0), false, 0),
      new(ColorSchemaNameNormalLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), false, 10),
      new(ColorSchemaNameNormalGlowLocKey, HexColor(0x00DAB9), HexColor(0x00B196), HexColor(0x00FFD9), true, 10),
      new(ColorSchemaNameBwDarkLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), false, 10),
      new(ColorSchemaNameBwBrightLocKey, HexColor(0xCACACA), HexColor(0xCACACA), HexColor(0xFFFFFF), false, 21),
  ];

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

  internal static Action OnSettingsUpdated;

  public LimitedStringModSetting ColorSchemaInternal { get; } =
    new(
        0,
        SchemaPresets.Select(p => new LimitedStringModSettingValue(p.NameLocKey, p.NameLocKey)).ToArray(),
        ModSettingDescriptor.CreateLocalized(ColorSchemaDropdownLocKey));

  public ColorModSetting GrassColor { get; } =
      new(new Color(0f, 1f, 0.851f),
          ModSettingDescriptor.CreateLocalized(ColorGrassLocKey).SetEnableCondition(IsCustom),
          false);

  public ColorModSetting CliffColor { get; } =
      new(new Color(0.349f, 0.451f, 0.380f),
          ModSettingDescriptor.CreateLocalized(ColorCliffLocKey).SetEnableCondition(IsCustom),
          false);

  public ColorModSetting CliffEdgeColor { get; } =
      new(new Color(0f, 1f, 0.851f),
          ModSettingDescriptor.CreateLocalized(ColorCliffEdgeLocKey).SetEnableCondition(IsCustom),
          false);

  public ModSetting<int> GhostModeIntensity { get; } =
      new RangeIntModSetting(
          50, 0, 100, ModSettingDescriptor.CreateLocalized(GhostModeIntensityLocKey).SetEnableCondition(IsCustom));

  public ModSetting<bool> GlowingEdges { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(GlowingLocKey).SetEnableCondition(IsCustom));

  #endregion

  #region Implementation

  static ColorSettings _instance;

  ColorSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
    OnSettingsUpdated = null;
    InstallSettingCallback(ColorSchemaInternal, ApplyColorSchema);
    GrassColor.ValueChanged += (_, _) => OnSettingsUpdated?.Invoke();
    CliffColor.ValueChanged += (_, _) => OnSettingsUpdated?.Invoke();
    CliffEdgeColor.ValueChanged += (_, _) => OnSettingsUpdated?.Invoke();
    GlowingEdges.ValueChanged += (_, _) => OnSettingsUpdated?.Invoke();
    GhostModeIntensity.ValueChanged += (_, _) => OnSettingsUpdated?.Invoke();
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
  }

  static bool IsCustom() => _instance.ColorSchemaInternal.Value == ColorSchemaNameCustomLocKey;

  static Color HexColor(int colorIndex) {
    return new Color(colorIndex >> 16 & 0xFF, colorIndex >> 8 & 0xFF, colorIndex & 0xFF) / 255f; 
  }

  #endregion
}
