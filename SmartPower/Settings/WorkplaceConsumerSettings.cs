// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class WorkplaceConsumerSettings : BaseSettings<WorkplaceConsumerSettings> {

  const string HeaderStringLocKey = "IgorZ.SmartPower.Settings.UnmannedConsumerSection";
  const string ShowFloatingIconLocKey = "IgorZ.SmartPower.Settings.UI.ShowFloatingIcon";
  const string SuspendDelayMinutesLocKey = "IgorZ.SmartPower.Settings.Hysteresis.SuspendDelay";
  const string ResumeDelayMinutesLocKey = "IgorZ.SmartPower.Settings.Hysteresis.ResumeDelay";

  protected override string ModId => Configurator.ModId;
    
  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowFloatingIcon { get; private set; }
  public ModSetting<bool> ShowFloatingIconInternal { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(ShowFloatingIconLocKey));

  public static int SuspendDelayMinutes { get; private set; }
  public ModSetting<int> SuspendDelayMinutesInternal { get; } =
      new RangeIntModSetting(60, 0, 120, ModSettingDescriptor.CreateLocalized(SuspendDelayMinutesLocKey));

  public static int ResumeDelayMinutes { get; private set; }
  public ModSetting<int> ResumeDelayMinutesInternal { get; } =
      new RangeIntModSetting(60, 0, 120, ModSettingDescriptor.CreateLocalized(ResumeDelayMinutesLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 4;

  #endregion

  WorkplaceConsumerSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(ShowFloatingIconInternal, v => ShowFloatingIcon = v);
    InstallSettingCallback(SuspendDelayMinutesInternal, v => SuspendDelayMinutes = v);
    InstallSettingCallback(ResumeDelayMinutesInternal, v => ResumeDelayMinutes = v);
  }
}
