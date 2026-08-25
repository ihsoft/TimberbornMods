// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class InjuryProbabilitySettings(
        ISettings settings,
        ModSettingsOwnerRegistry modSettingsOwnerRegistry,
        ModRepository modRepository) : ModSettingsOwner(settings, modSettingsOwnerRegistry, modRepository) {

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable InconsistentNaming

  public ModSetting<bool> ShowInFragment { get; } = new(
      false, ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.InjuryProbability.ShowInFragment"));

  public ModSetting<bool> ShowAvatarHint { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.TimberCommons.Settings.InjuryProbability.ShowAvatarHint")
          .SetLocalizedTooltip("IgorZ.TimberCommons.Settings.InjuryProbability.ShowAvatarHintTooltip"));

  public ModSetting<bool> ShowDailyProbability { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.InjuryProbability.ShowAsDailyProbability"));

  // ReSharper restore InconsistentNaming
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.InjuryProbabilitySection";

  /// <inheritdoc />
  public override int Order => 4;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion
}
