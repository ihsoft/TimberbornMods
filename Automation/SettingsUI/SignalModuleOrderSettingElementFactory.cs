// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using ModSettings.Core;
using ModSettings.CoreUI;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.SettingsUI;

sealed class SignalModuleOrderSettingElementFactory(
    UiFactory uiFactory, IContainer dependencyContainer, IModSettingsContextProvider contextProvider,
    ITooltipRegistrar tooltipRegistrar) : IModSettingElementFactory {

  const string SettingElementAsset = "IgorZ.Automation/SignalModuleOrderSettingElement";
  const string UnavailableLocKey = "IgorZ.Automation.Settings.ScriptEditor.SignalModuleOrderUnavailable";

  #region API

  /// <inheritdoc />
  public int Priority => 1000;

  /// <inheritdoc />
  public bool TryCreateElement(ModSetting modSetting, out IModSettingElement element) {
    if (modSetting is not SignalModuleOrderSetting signalModuleOrderSetting) {
      element = null;
      return false;
    }

    var root = uiFactory.LoadVisualElement(SettingElementAsset);
    var descriptor = signalModuleOrderSetting.Descriptor;
    root.Q2<Label>("SettingLabel").text = descriptor.NameLocKey != null
        ? uiFactory.T(descriptor.NameLocKey)
        : descriptor.Name;

    var configureButton = root.Q2<Button>("ConfigureButton");
    var isAvailable = contextProvider.Context.HasFlag(ModSettingsContext.Game);
    configureButton.SetEnabled(isAvailable);
    if (isAvailable) {
      configureButton.clicked += () => dependencyContainer.GetInstance<SignalModuleOrderDialog>().Show();
      if (descriptor.TooltipLocKey != null) {
        tooltipRegistrar.RegisterLocalizable(root, descriptor.TooltipLocKey);
      }
    } else {
      tooltipRegistrar.RegisterLocalizable(root, UnavailableLocKey);
    }

    element = new SignalModuleOrderSettingElement(root, signalModuleOrderSetting);
    return true;
  }

  #endregion

  #region Implementation

  sealed class SignalModuleOrderSettingElement(VisualElement root, ModSetting modSetting) : IModSettingElement {
    /// <inheritdoc />
    public VisualElement Root { get; } = root;

    /// <inheritdoc />
    public bool ShouldBlockInput => false;

    /// <inheritdoc />
    public ModSetting ModSetting { get; } = modSetting;
  }

  #endregion
}
