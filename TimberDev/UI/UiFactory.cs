// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberDev.UI {

/// <summary>Factory for making standard fragment panel elements.</summary>
public class UiFactory {
  readonly VisualElementLoader _visualElementLoader;

  /// <summary>The localization service to use for the UI elements.</summary>
  public ILoc Loc { get; }

  UiFactory(VisualElementLoader visualElementLoader, ILoc loc) {
    _visualElementLoader = visualElementLoader;
    Loc = loc;
  }

  /// <summary>Creates a slider in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// TAPI offers the sliders builder, but in the recent updates it got broken. Use this factory as a quick workaround.
  /// </remarks>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="stepSize">
  /// The minimum delta for the value changes. All positions on the slider will be multiples of this value.
  /// </param>
  /// <param name="lowValue">The lowest possible value.</param>
  /// <param name="highValue">The highest possible value.</param>
  public Slider CreateSlider(Action<float> onValueChangedFn,
                             float stepSize = 0.05f, float lowValue = 0, float highValue = 1.0f) {
    var slider = _visualElementLoader.LoadVisualElement("Common/IntegerSlider").Q<Slider>("Slider");
    slider.lowValue = lowValue;
    slider.highValue = highValue;
    slider.RegisterValueChangedCallback(
        _ => {
          var value = Mathf.Round(slider.value / stepSize) * stepSize;
          slider.SetValueWithoutNotify(value);
          onValueChangedFn(value);
        });
    return slider;
  }
  
  /// <summary>Creates a toggle in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the caption.</param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  public Toggle CreateToggle(string locKey, Action<bool> onValueChangedFn) {
    var toggle = _visualElementLoader.LoadVisualElement("Game/EntityPanel/HaulCandidateFragment").Q<Toggle>("Toggle");
    toggle.text = Loc.T(locKey);
    toggle.RegisterValueChangedCallback(
        _ => {
          onValueChangedFn(toggle.value);
        });
    return toggle;
  }

  /// <summary>Creates a label in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Optional loc key for the caption.</param>
  public Label CreateLabel(string locKey = null) {
    var label = _visualElementLoader.LoadVisualElement("Game/EntityPanel/MechanicalNodeFragment").Q<Label>("Generator");
    if (locKey != null) {
      label.text = Loc.T(locKey);
    }
    return label;
  }

  /// <summary>Creates a panel that can be used as a fragment in the right side panel.</summary>
  /// <remarks>
  /// This is a root element for the fragment's panel. Add controls to it via <see cref="VisualElement.Add"/>
  /// </remarks>
  public VisualElement CreateFragmentPanel() {
    var panel = _visualElementLoader.LoadVisualElement("Game/EntityPanel/HaulCandidateFragment");
    panel.Clear();
    return panel;
  }
}

}
