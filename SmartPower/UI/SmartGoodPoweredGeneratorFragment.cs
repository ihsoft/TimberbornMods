// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower.UI {

/// <summary>UI fragment that controls <see cref="SmartGoodPoweredGenerator"/> settings.</summary>
sealed class SmartGoodPoweredGeneratorFragment : IEntityPanelFragment {
  const string NeverStopThisGeneratorLocKey = "IgorZ.SmartPower.PoweredGenerator.NeverStop";
  static readonly Color NormalColor = new(0.8f, 0.8f, 0.8f);

  readonly UIBuilder _builder;
  VisualElement _root;
  Toggle _neverShutdownCheckbox;
  SmartGoodPoweredGenerator _generator;

  public SmartGoodPoweredGeneratorFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _neverShutdownCheckbox = _builder.Presets()
        .Toggles()
        .CheckmarkInverted(locKey: NeverStopThisGeneratorLocKey, color: NormalColor);
    _neverShutdownCheckbox.RegisterValueChangedCallback(_ => _generator.NeverShutdown = _neverShutdownCheckbox.value);
    var builder = _builder.CreateFragmentBuilder().AddComponent(_neverShutdownCheckbox);
    _root = builder.BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _generator = entity.GetComponentFast<SmartGoodPoweredGenerator>();
    _root.ToggleDisplayStyle(visible: _generator != null);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _generator = null;
  }

  public void UpdateFragment() {
    if (_generator != null) {
      _neverShutdownCheckbox.SetValueWithoutNotify(_generator.NeverShutdown);
    }
  }
}

}
