// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class WaterBuildingsSettings : BaseSettings<WaterBuildingsSettings> {
  const string HeaderStringLocKey = "IgorZ.TimberCommons.Settings.WaterBuildingsSection";
  const string AdjustWaterDepthAtSpillwayOnMechanicalPumpsLocKey =
      "IgorZ.TimberCommons.Settings.WaterBuildings.AdjustWaterDepthAtSpillwayOnMechanicalPumps";
  const string AdjustWaterDepthAtSpillwayOnFluidDumpsLocKey =
      "IgorZ.TimberCommons.Settings.WaterBuildings.AdjustWaterDepthAtSpillwayOnFluidDumps";
  const string UseLocalOutputLevelLimitScanLocKey =
      "IgorZ.TimberCommons.Settings.WaterBuildings.UseLocalOutputLevelLimitScan";
  const string OutputLevelLimitScanRadiusLocKey =
      "IgorZ.TimberCommons.Settings.WaterBuildings.OutputLevelLimitScanRadius";
  const string OutputLevelLimitScanRadiusTooltipLocKey =
      "IgorZ.TimberCommons.Settings.WaterBuildings.OutputLevelLimitScanRadiusTooltip";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global

  public static bool AdjustWaterDepthAtSpillwayOnMechanicalPumps { get; private set; } = true;
  public ModSetting<bool> AdjustWaterDepthAtSpillwayOnMechanicalPumpsInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(AdjustWaterDepthAtSpillwayOnMechanicalPumpsLocKey));

  public static bool AdjustWaterDepthAtSpillwayOnFluidDumps { get; private set; } = true;
  public ModSetting<bool> AdjustWaterDepthAtSpillwayOnFluidDumpsInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(AdjustWaterDepthAtSpillwayOnFluidDumpsLocKey));

  public static bool UseLocalOutputLevelLimitScan { get; private set; } = true;
  public ModSetting<bool> UseLocalOutputLevelLimitScanInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(UseLocalOutputLevelLimitScanLocKey));

  public static int OutputLevelLimitScanRadius { get; private set; } = 8;
  public ModSetting<int> OutputLevelLimitScanRadiusInternal { get; } = new RangeIntModSetting(
      8, 1, 64,
      ModSettingDescriptor.CreateLocalized(OutputLevelLimitScanRadiusLocKey)
          .SetLocalizedTooltip(OutputLevelLimitScanRadiusTooltipLocKey)
          .SetEnableCondition(() => UseLocalOutputLevelLimitScan));

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 3;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  public WaterBuildingsSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(
        AdjustWaterDepthAtSpillwayOnMechanicalPumpsInternal, v => AdjustWaterDepthAtSpillwayOnMechanicalPumps = v);
    InstallSettingCallback(
        AdjustWaterDepthAtSpillwayOnFluidDumpsInternal, v => AdjustWaterDepthAtSpillwayOnFluidDumps = v);
    InstallSettingCallback(UseLocalOutputLevelLimitScanInternal, v => UseLocalOutputLevelLimitScan = v);
    InstallSettingCallback(OutputLevelLimitScanRadiusInternal, v => OutputLevelLimitScanRadius = v);
  }

  #endregion
}
