// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.XRay.Core;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.InputSystemUI;
using Timberborn.KeyBindingSystemUI;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.XRay.CoreUI;

sealed class XRayModeTogglePanel : ILoadableSingleton {
  const string ToggleModeButtonImage = "UI/Images/Game/IgorZ.XRay/square-toggle-xray-mode";
  const string ShowGridRenderingLocKey = "IgorZ.XRay.Visibility.Show";
  const string HideGridRenderingLocKey = "IgorZ.XRay.Visibility.Hide";

  public void Load() {
    _root = _visualElementLoader.LoadVisualElement("Common/SquareToggle");
    _tooltipRegistrar.Register(_root, GetTooltip);
    _toggle = _root.Q<Toggle>("Toggle");
    // The normal way with stylesheet doesn't work. The style gets broken on game reload. No clue why.
    var chkMark = _toggle.Q<VisualElement>("unity-checkmark");
    chkMark.style.backgroundImage = new StyleBackground(_assetLoader.Load<Sprite>(ToggleModeButtonImage));
    _bindableToggleFactory.CreateAndBind(
        _toggle, KeyBindingInputProcessor.ToggleModeBindingKey, OnGridToggled, () => _xRayModeManager.IsActive);
    _uiLayout.AddTopRightButton(_root, 10);
  }

  readonly XRayModeManager _xRayModeManager;
  readonly VisualElementLoader _visualElementLoader;
  readonly UILayout _uiLayout;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly BindableToggleFactory _bindableToggleFactory;
  readonly KeyBindingTooltipFactory _keyBindingTooltipFactory;
  readonly IAssetLoader _assetLoader;

  VisualElement _root;
  Toggle _toggle;

  XRayModeTogglePanel(
      XRayModeManager xRayModeManager, VisualElementLoader visualElementLoader, UILayout uiLayout,
      ITooltipRegistrar tooltipRegistrar, BindableToggleFactory bindableToggleFactory,
      KeyBindingTooltipFactory keyBindingTooltipFactory, IAssetLoader assetLoader) {
    _xRayModeManager = xRayModeManager;
    _visualElementLoader = visualElementLoader;
    _uiLayout = uiLayout;
    _tooltipRegistrar = tooltipRegistrar;
    _bindableToggleFactory = bindableToggleFactory;
    _keyBindingTooltipFactory = keyBindingTooltipFactory;
    _assetLoader = assetLoader;
  }

  string GetTooltip() {
    var headerLocKey = _xRayModeManager.IsActive ? HideGridRenderingLocKey : ShowGridRenderingLocKey;
    return _keyBindingTooltipFactory.Create(
        headerLocKey, KeyBindingInputProcessor.ToggleModeBindingKey, KeyBindingInputProcessor.ShowModeBindingKey);
  }

  void OnGridToggled(bool toggleState) {
    _xRayModeManager.SetActiveMode(toggleState);
  }
}
