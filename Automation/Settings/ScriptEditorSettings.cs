// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
sealed class ScriptEditorSettings : BaseSettings<ScriptEditorSettings> {

  const string HeaderStringLocKey = "IgorZ.Automation.Settings.ScriptEditor.Header";
  const string DefaultScriptSyntaxLocKey = "IgorZ.Automation.Settings.ScriptEditor.DefaultScriptSyntax";
  const string DefaultScriptSyntaxLispLocKey = "IgorZ.Automation.Settings.ScriptEditor.DefaultScriptSyntaxLisp";
  const string DefaultScriptSyntaxPythonLocKey = "IgorZ.Automation.Settings.ScriptEditor.DefaultScriptSyntaxPython";
  const string DefaultScriptSyntaxTooltipLocKey = "IgorZ.Automation.Settings.ScriptEditor.DefaultScriptSyntaxTooltip";
  const string SignalModuleOrderLocKey = "IgorZ.Automation.Settings.ScriptEditor.SignalModuleOrder";
  const string SignalModuleOrderTooltipLocKey = "IgorZ.Automation.Settings.ScriptEditor.SignalModuleOrderTooltip";

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

  public enum ScriptSyntax {
    Python,
    Lisp,
  }

  public static ScriptSyntax DefaultScriptSyntax { get; private set; } = ScriptSyntax.Python;
  public LimitedStringModSetting DefaultScriptSyntaxInternal { get; } = new(
      0, [
          new LimitedStringModSettingValue(nameof(ScriptSyntax.Python), DefaultScriptSyntaxPythonLocKey),
          new LimitedStringModSettingValue(nameof(ScriptSyntax.Lisp), DefaultScriptSyntaxLispLocKey),
      ], ModSettingDescriptor.CreateLocalized(DefaultScriptSyntaxLocKey)
          .SetLocalizedTooltip(DefaultScriptSyntaxTooltipLocKey));

  /// <summary>Serialized module order displayed through the custom settings element.</summary>
  public SignalModuleOrderSetting SignalModuleOrderInternal { get; } = new(
      "", ModSettingDescriptor.CreateLocalized(SignalModuleOrderLocKey)
          .SetLocalizedTooltip(SignalModuleOrderTooltipLocKey));

  #endregion

  #region API

  /// <summary>Saves a complete custom order of scriptable module names.</summary>
  public static void SetSignalModuleOrder(IEnumerable<string> moduleNames) {
    Instance.SignalModuleOrderInternal.SetValue(SignalModuleOrder.Serialize(moduleNames));
  }

  /// <summary>Restores the component registration order.</summary>
  public static void ResetSignalModuleOrder() {
    Instance.SignalModuleOrderInternal.SetValue("");
  }

  #endregion

  #region Implementation

  ScriptEditorSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(DefaultScriptSyntaxInternal, v => {
      DefaultScriptSyntax = (ScriptSyntax)Enum.Parse(typeof(ScriptSyntax), DefaultScriptSyntaxInternal.Value);
    });
    InstallSettingCallback(SignalModuleOrderInternal, SignalModuleOrder.Load);
  }

  #endregion
}
