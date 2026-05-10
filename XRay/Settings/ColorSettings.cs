// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics.CodeAnalysis;
using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityEngine;

namespace IgorZ.XRay.Settings;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
sealed class ColorSettings : BaseSettings<ColorSettings> {

  const string HeaderStringLocKey = "IgorZ.XRay.ColorSettings.Header";
  const string GrassColorLocKey = "IgorZ.XRay.ColorSettings.GrassColor";
  const string CliffColorLocKey = "IgorZ.XRay.ColorSettings.CliffColor";
  const string CliffEdgeColorLocKey = "IgorZ.XRay.ColorSettings.CliffEdgeColor";
  const string GlowingLocKey = "IgorZ.XRay.ColorSettings.Glowing";
  const string GhostModeIntensityLocKey = "IgorZ.XRay.ColorSettings.GhostModelIntensity";

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

  public static Color GrassColor { get; private set; }
  public static Color CliffColor { get; private set; }
  public static Color CliffEdgeColor { get; private set; }
  public static Color GlowGrassColor { get; private set; }
  public static Color GlowCliffColor { get; private set; }
  public static Color GlowCliffEdgeColor { get; private set; }

  public ColorModSetting GrassColorInternal { get; } =
      new(new Color(0f, 1f, 0.851f), ModSettingDescriptor.CreateLocalized(GrassColorLocKey), false);

  public ColorModSetting CliffColorInternal { get; } =
      new(new Color(0.349f, 0.451f, 0.380f), ModSettingDescriptor.CreateLocalized(CliffColorLocKey), false);

  public ColorModSetting CliffEdgeColorInternal { get; } =
      new(new Color(0f, 1f, 0.851f), ModSettingDescriptor.CreateLocalized(CliffEdgeColorLocKey), false);

  public ModSetting<int> GhostModeIntensityInternal { get; } =
      new RangeIntModSetting(50, 0, 100, ModSettingDescriptor.CreateLocalized(GhostModeIntensityLocKey));

  public static bool GlowingEdges { get; private set; }
  public ModSetting<bool> GlowingEdgesInternal { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(GlowingLocKey));

  #endregion

  #region Implementation

  const float GrassTransparencyBase = 0.08235f;
  const float CliffTransparencyBase = 0.11765f;
  const float CliffEdgeTransparencyBase = 0.14118f;

  ColorSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    OnSettingsUpdated = null;
    InstallSettingCallback(GrassColorInternal, UpdateColors);
    InstallSettingCallback(CliffColorInternal, UpdateColors);
    InstallSettingCallback(CliffEdgeColorInternal, UpdateColors);
    InstallSettingCallback(GlowingEdgesInternal, UpdateColors);
    InstallSettingCallback(GhostModeIntensityInternal, UpdateColors);
  }

  void UpdateColors() {
    GlowingEdges = GlowingEdgesInternal.Value;
    var intensity = GhostModeIntensityInternal.Value / 100f;
    GrassColor = new Color(
        GrassColorInternal.Color.r, GrassColorInternal.Color.g, GrassColorInternal.Color.b,
        intensity + GrassTransparencyBase);
    CliffColor = new Color(
        CliffColorInternal.Color.r, CliffColorInternal.Color.g, CliffColorInternal.Color.b,
        intensity + CliffTransparencyBase);
    CliffEdgeColor = new Color(
        CliffEdgeColorInternal.Color.r, CliffEdgeColorInternal.Color.g, CliffEdgeColorInternal.Color.b,
        intensity + CliffEdgeTransparencyBase);
    GlowGrassColor = GrassColorInternal.Color * intensity;
    GlowCliffColor = CliffColorInternal.Color * intensity;
    GlowCliffEdgeColor = CliffEdgeColorInternal.Color * intensity;
    OnSettingsUpdated?.Invoke();
  }

  #endregion
}
