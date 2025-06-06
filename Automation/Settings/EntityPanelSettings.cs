﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

sealed class EntityPanelSettings : ModSettingsOwner {

  const string HeaderStringLocKey = "IgorZ.Automation.Settings.EntityPanel.Header";
  const string AlwaysShowAddRulesButtonLocKey = "IgorZ.Automation.Settings.EntityPanel.AlwaysShowAddRulesButton";
  const string RulesDescriptionStyleLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle";
  const string DescriptionHumanReadableLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.HumanReadable";
  const string DescriptionScriptLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.Script";
  const string DescriptionScriptShortLocKey = "IgorZ.Automation.Settings.EntityPanel.RulesDescriptionStyle.ScriptShort";

  protected override string ModId => Configurator.AutomationModId;

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public ModSetting<bool> AlwaysShowAddRulesButton { get; } =
    new(false, ModSettingDescriptor.CreateLocalized(AlwaysShowAddRulesButtonLocKey));

  public enum DescriptionStyle {
    HumanReadable,
    Script,
    ScriptShort,
  }

  public DescriptionStyle RulesDescriptionStyle { get; private set; }

  public LimitedStringModSetting _rulesDescriptionStyle { get; } = new(
      0, [
          new LimitedStringModSettingValue(nameof(DescriptionStyle.HumanReadable), DescriptionHumanReadableLocKey),
          new LimitedStringModSettingValue(nameof(DescriptionStyle.Script), DescriptionScriptLocKey),
          new LimitedStringModSettingValue(nameof(DescriptionStyle.ScriptShort), DescriptionScriptShortLocKey),
      ], ModSettingDescriptor.CreateLocalized(RulesDescriptionStyleLocKey));

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
    _rulesDescriptionStyle.ValueChanged += (_, _) => {
      RulesDescriptionStyle = (DescriptionStyle)Enum.Parse(typeof(DescriptionStyle), _rulesDescriptionStyle.Value);
    };
  }
}
