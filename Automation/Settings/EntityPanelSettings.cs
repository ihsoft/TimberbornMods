// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

sealed class EntityPanelSettings : BaseSettings<EntityPanelSettings> {

  const string HeaderStringLocKey = "IgorZ.Automation.Settings.EntityPanel.Header";
  const string RulesDescriptionStyleLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle";
  const string DescriptionHumanReadableLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.HumanReadable";
  const string DescriptionScriptLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.Script";
  const string DescriptionScriptShortLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.ScriptShort";
  const string EvalValuesInConditionsLocKey = "IgorZ.Automation.Settings.EntityPanel.EvalValuesInConditions";
  const string EvalValuesInActionArgumentsLocKey = "IgorZ.Automation.Settings.EntityPanel.EvalValuesInActionArguments";
  const string MaxRulesShownLocKey = "IgorZ.Automation.Settings.EntityPanel.MaxRulesShown";

  protected override string ModId => Configurator.AutomationModId;

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public enum DescriptionStyle {
    HumanReadable,
    Script,
    ScriptShort,
  }

  public static DescriptionStyle RulesDescriptionStyle { get; private set; }
  public LimitedStringModSetting RulesDescriptionStyleInternal { get; } = new(
      0, [
          new LimitedStringModSettingValue(nameof(DescriptionStyle.HumanReadable), DescriptionHumanReadableLocKey),
          new LimitedStringModSettingValue(nameof(DescriptionStyle.Script), DescriptionScriptLocKey),
          new LimitedStringModSettingValue(nameof(DescriptionStyle.ScriptShort), DescriptionScriptShortLocKey),
      ], ModSettingDescriptor.CreateLocalized(RulesDescriptionStyleLocKey));

  public static bool EvalValuesInConditions { get; private set; }
  public ModSetting<bool> EvalValuesInConditionsInternal { get; } =
      new(false, ModSettingDescriptor.CreateLocalized(EvalValuesInConditionsLocKey));

  public static bool EvalValuesInActionArguments { get; private set; }
  public ModSetting<bool> EvalValuesInActionArgumentsInternal { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(EvalValuesInActionArgumentsLocKey));

  public static int MaxRulesShown { get; private set; }
  public ModSetting<int> MaxRulesShownInternal { get; } =
      new(6, ModSettingDescriptor.CreateLocalized(MaxRulesShownLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 0;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  public EntityPanelSettings(ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry,
                             ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(RulesDescriptionStyleInternal, v => {
      RulesDescriptionStyle =
          (DescriptionStyle)Enum.Parse(typeof(DescriptionStyle), RulesDescriptionStyleInternal.Value);
    });
    InstallSettingCallback(EvalValuesInConditionsInternal, v => EvalValuesInConditions = v);
    InstallSettingCallback(EvalValuesInActionArgumentsInternal, v => EvalValuesInActionArguments = v);
    InstallSettingCallback(MaxRulesShownInternal, v => MaxRulesShown = v);
  }
}
