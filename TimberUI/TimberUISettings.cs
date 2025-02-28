// Timberborn Mod: TimberUI
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberUI;

sealed class TimberUISettings(
    ISettings settings,
    ModSettingsOwnerRegistry modSettingsOwnerRegistry,
    ModRepository modRepository) : ModSettingsOwner(settings, modSettingsOwnerRegistry, modRepository) {

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable InconsistentNaming

  public ModSetting<string> TargetPath { get; } = new("", ModSettingDescriptor.Create("Base save path"));

  // ReSharper restore InconsistentNaming
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  //public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.WaterBuildingsSection";

  /// <inheritdoc />
  //public override int Order => 3;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu;

  #endregion

  #region Implementation

  #endregion
}
