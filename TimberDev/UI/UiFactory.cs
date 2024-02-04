// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.CoreUI;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.UI {

static class UiFactory {
  /// <summary>Color for the normal text in building details panel.</summary>
  public static readonly Color PanelNormalColor = new(0.8f, 0.8f, 0.8f);

  /// <summary>Creates a slider in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// TAPI offers the sliders builder, but in the recent updates it got broken. Use this factory as a quick workaround.
  /// </remarks>
  /// <param name="visualElementLoader">The loader to access the game's assets.</param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="stepSize">
  /// The minimum delta for the value changes. All positions on the slider will be multiples of this value.
  /// </param>
  /// <param name="lowValue">The lowest possible value.</param>
  /// <param name="highValue">The highest possible value.</param>
  public static Slider CreateSlider(VisualElementLoader visualElementLoader, Action<float> onValueChangedFn,
                                    float stepSize = 0.05f, float lowValue = 0, float highValue = 1.0f) {
    var slider = visualElementLoader.LoadVisualElement("Common/IntegerSlider").Q<Slider>("Slider");
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
}

}
