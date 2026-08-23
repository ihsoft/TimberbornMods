// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityEngine;

namespace IgorZ.XRay.Settings;

sealed class ObjectTransparencySettings : BaseSettings<ObjectTransparencySettings> {

  const string BuildingColorLocKey = "IgorZ.XRay.ObjectTransparencySettings.BuildingColor";
  const string BuildingTransparencyLocKey = "IgorZ.XRay.ObjectTransparencySettings.BuildingTransparency";
  const string HeaderStringLocKey = "IgorZ.XRay.ObjectTransparencySettings.Header";
  const string PlantColorLocKey = "IgorZ.XRay.ObjectTransparencySettings.PlantColor";
  const string PlantTransparencyLocKey = "IgorZ.XRay.ObjectTransparencySettings.PlantTransparency";

  static readonly Color DefaultColor = new(0.8f, 0.8f, 0.8f);

  protected override string ModId => Configurator.AutomationModId;

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 1;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Settings

  public ColorModSetting BuildingColor { get; } =
      new(DefaultColor, ModSettingDescriptor.CreateLocalized(BuildingColorLocKey), false);

  public ModSetting<int> BuildingTransparency { get; } = new RangeIntModSetting(
      85, 0, 100, ModSettingDescriptor.CreateLocalized(BuildingTransparencyLocKey));

  public ColorModSetting PlantColor { get; } =
      new(DefaultColor, ModSettingDescriptor.CreateLocalized(PlantColorLocKey), false);

  public ModSetting<int> PlantTransparency { get; } = new RangeIntModSetting(
      85, 0, 100, ModSettingDescriptor.CreateLocalized(PlantTransparencyLocKey));

  public Color BuildingFillColor {
    get {
      var color = BuildingColor.Color;
      color.a = 1f - BuildingTransparency.Value / 100f;
      return color;
    }
  }

  public Color PlantFillColor {
    get {
      var color = PlantColor.Color;
      color.a = 1f - PlantTransparency.Value / 100f;
      return color;
    }
  }

  #endregion

  #region Implementation

  ObjectTransparencySettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
  }

  #endregion
}
