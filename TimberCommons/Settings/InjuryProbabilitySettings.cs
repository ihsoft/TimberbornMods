// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class InjuryProbabilitySettings : BaseSettings<InjuryProbabilitySettings> {
  const string HeaderStringLocKey = "IgorZ.TimberCommons.Settings.InjuryProbabilitySection";
  const string ShowInFragmentLocKey = "IgorZ.TimberCommons.Settings.InjuryProbability.ShowInFragment";
  const string ShowAvatarHintLocKey = "IgorZ.TimberCommons.Settings.InjuryProbability.ShowAvatarHint";
  const string ShowAvatarHintTooltipLocKey =
      "IgorZ.TimberCommons.Settings.InjuryProbability.ShowAvatarHintTooltip";
  const string ShowDailyProbabilityLocKey =
      "IgorZ.TimberCommons.Settings.InjuryProbability.ShowAsDailyProbability";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowInFragment { get; private set; }
  public ModSetting<bool> ShowInFragmentInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(ShowInFragmentLocKey));

  public static bool ShowAvatarHint { get; private set; } = true;
  public ModSetting<bool> ShowAvatarHintInternal { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized(ShowAvatarHintLocKey)
          .SetLocalizedTooltip(ShowAvatarHintTooltipLocKey));

  public static bool ShowDailyProbability { get; private set; } = true;
  public ModSetting<bool> ShowDailyProbabilityInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(ShowDailyProbabilityLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 4;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  public InjuryProbabilitySettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(ShowInFragmentInternal, v => ShowInFragment = v);
    InstallSettingCallback(ShowAvatarHintInternal, v => ShowAvatarHint = v);
    InstallSettingCallback(ShowDailyProbabilityInternal, v => ShowDailyProbability = v);
  }

  #endregion
}
