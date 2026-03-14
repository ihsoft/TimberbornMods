// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Settings;

/// <summary>Base class for the mod settings.</summary>
/// <remarks>
/// Inherit from this class and add an injection in the configurator. Use
/// <see cref="InstallSettingCallback{TV}(ModSetting{TV},Action{TV})"/> to create a static property that will update to
/// the setting value.
/// </remarks>
/// <code><![CDATA[
/// // Static setting property.
/// public static bool MyProperty { get; private set; }
/// public ModSetting<bool> MyPropertyInternal { get; } =
///     new(true, ModSettingDescriptor.CreateLocalized("MyPropertyLocKey"));
/// // Init static property.
/// MySettingsCosntructor(ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry,
///     ModRepository modRepository) : base(settings, modSettingsOwnerRegistry, modRepository) {
///   InstallSettingCallback(MyPropertyInternal, v => MyProperty = v);
/// }
/// ]]></code>
/// <typeparam name="T"></typeparam>
abstract class BaseSettings<T> : ModSettingsOwner where T : BaseSettings<T> {

  protected static T Instance;

  /// <summary>Set up a callback that will get called on the setting update and on the game load.</summary>
  protected void InstallSettingCallback<TV>(ModSetting<TV> setting, Action<TV> setter) where TV : notnull {
    setting.ValueChanged += (_, _) => setter(setting.Value);
    _afterLoadActions.Add(() => setter(setting.Value));
  }

  /// <inheritdoc cref="InstallSettingCallback{TV}(ModSetting{TV}, Action{TV})"/>
  protected void InstallSettingCallback<TV>(ModSetting<TV> setting, Action setter) where TV : notnull {
    setting.ValueChanged += (_, _) => setter();
    _afterLoadActions.Add(setter);
  }

  /// <inheritdoc />
  protected override void OnAfterLoad() {
    base.OnAfterLoad();
    foreach (var action in _afterLoadActions) {
      action();
    }
    _afterLoadActions.Clear();
  }

  readonly List<Action> _afterLoadActions = [];

  protected BaseSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository) :
      base(settings, modSettingsOwnerRegistry, modRepository) {
    Instance = (T)this;
  }
}
