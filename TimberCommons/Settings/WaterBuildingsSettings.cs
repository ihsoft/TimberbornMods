// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class WaterBuildingsSettings : ModSettingsOwner {

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable InconsistentNaming

  public static bool AdjustWaterDepthAtSpillwayOnMechanicalPumps =>
      _instance._adjustWaterDepthAtSpillwayOnMechanicalPumps.Value;
  public ModSetting<bool> _adjustWaterDepthAtSpillwayOnMechanicalPumps { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.TimberCommons.Settings.WaterBuildings.AdjustWaterDepthAtSpillwayOnMechanicalPumps"));

  public static bool AdjustWaterDepthAtSpillwayOnFluidDumps =>
      _instance._adjustWaterDepthAtSpillwayOnFluidDumps.Value;

  public ModSetting<bool> _adjustWaterDepthAtSpillwayOnFluidDumps { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.TimberCommons.Settings.WaterBuildings.AdjustWaterDepthAtSpillwayOnFluidDumps"));

  public static bool ShowCurrentStrengthInSluice =>
      _instance._showCurrentStrengthInSluice.Value;
  public ModSetting<bool> _showCurrentStrengthInSluice { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.WaterBuildings.ShowCurrentStrengthInSluice"));

  // ReSharper restore InconsistentNaming
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.WaterBuildingsSection";

  /// <inheritdoc />
  public override int Order => 3;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  static WaterBuildingsSettings _instance;

  public WaterBuildingsSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
  }

  #endregion
}
