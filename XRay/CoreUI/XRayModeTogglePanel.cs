// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.XRay.Core;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.InputSystemUI;
using Timberborn.KeyBindingSystemUI;
using Timberborn.Localization;
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
  const string ObjectTransparencyTooltipLocKey = "IgorZ.XRay.Visibility.ObjectTransparencyTooltip";
  const string ToggleLocKey = "KeyBinding.Toggle";
  const string HoldLocKey = "KeyBinding.Hold";

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
  readonly KeyBindingDescriber _keyBindingDescriber;
  readonly ILoc _loc;
  readonly IAssetLoader _assetLoader;

  VisualElement _root;
  Toggle _toggle;

  XRayModeTogglePanel(
      XRayModeManager xRayModeManager, VisualElementLoader visualElementLoader, UILayout uiLayout,
      ITooltipRegistrar tooltipRegistrar, BindableToggleFactory bindableToggleFactory,
      KeyBindingDescriber keyBindingDescriber, ILoc loc, IAssetLoader assetLoader) {
    _xRayModeManager = xRayModeManager;
    _visualElementLoader = visualElementLoader;
    _uiLayout = uiLayout;
    _tooltipRegistrar = tooltipRegistrar;
    _bindableToggleFactory = bindableToggleFactory;
    _keyBindingDescriber = keyBindingDescriber;
    _loc = loc;
    _assetLoader = assetLoader;
  }

  string GetTooltip() {
    var headerLocKey = _xRayModeManager.IsActive ? HideGridRenderingLocKey : ShowGridRenderingLocKey;
    var lines = new List<string> { _loc.T(headerLocKey) };
    AddKeyBindingInfo(lines, KeyBindingInputProcessor.ToggleModeBindingKey, _loc.T(ToggleLocKey));
    AddKeyBindingInfo(lines, KeyBindingInputProcessor.ShowModeBindingKey, _loc.T(HoldLocKey));
    AddKeyBindingInfo(
        lines, KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey,
        _loc.T(ObjectTransparencyTooltipLocKey));
    return string.Join("\n", lines);
  }

  void AddKeyBindingInfo(List<string> lines, string bindingKey, string description) {
    if (_keyBindingDescriber.TryGetKeyBindingText(bindingKey, out var bindingText)) {
      lines.Add($"{description} <b>{bindingText}</b>");
    }
  }

  void OnGridToggled(bool toggleState) {
    _xRayModeManager.SetActiveMode(toggleState);
  }
}
