// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
sealed class ScriptEngineSettings : BaseSettings<ScriptEngineSettings> {

  const string HeaderStringLocKey = "IgorZ.Automation.Settings.ScriptEngine.Header";
  const string CheckOptionsArgumentsLocKey = "IgorZ.Automation.Settings.ScriptEngine.CheckOptionsArguments";
  const string CheckArgumentValuesLocKey = "IgorZ.Automation.Settings.ScriptEngine.CheckArgumentValues";
  const string SignalExecutionStackSizeLocKey = "IgorZ.Automation.Settings.ScriptEngine.SignalExecutionStackSize";

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

  public ModSetting<bool> CheckOptionsArgumentsInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(CheckOptionsArgumentsLocKey));
  public static bool CheckOptionsArguments => Instance.CheckOptionsArgumentsInternal.Value;

  public static bool CheckArgumentValues { get; private set; }
  public ModSetting<bool> CheckArgumentValuesInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(CheckArgumentValuesLocKey));

  public static int SignalExecutionStackSize { get; private set; }
  public ModSetting<int> SignalExecutionStackSizeInternal { get; } = new(
      10, ModSettingDescriptor.CreateLocalized(SignalExecutionStackSizeLocKey));

  #endregion

  #region Implementation

  ScriptEngineSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) { 
    InstallSettingCallback(CheckArgumentValuesInternal, v => CheckArgumentValues = v);
    InstallSettingCallback(SignalExecutionStackSizeInternal, v => SignalExecutionStackSize = v);
  }

  #endregion
}
