// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

using System;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

/// <summary>Wrapper for the <see cref="PreciseSlider"/> that allows to set the step size for the slider.</summary>
/// <seealso cref="UiFactory.CreatePreciseSlider"/>
public class PreciseSliderWrapper : VisualElement {
  readonly PreciseSlider _slider;
  readonly Action<float> _onValueChangedFn;
  readonly float _stepSize;

  bool _sliderInitialization;

  internal PreciseSliderWrapper(PreciseSlider slider, Action<float> onValueChangedFn, float stepSize) {
    _slider = slider;
    _onValueChangedFn = onValueChangedFn;
    _stepSize = stepSize;
    slider.Initialize(OnWaterLevelSliderChanged, stepSize);
    Add(slider);
  }

  /// <summary>Gets the current value of the slider.</summary>
  public float Value => _slider.Value;

  /// <summary>Sets the value of the slider without invoking the value change callback.</summary>
  public void SetValueWithoutNotify(float value) => _slider.SetValueWithoutNotify(value);

  /// <summary>Sets the value of the slider and the maximum value without invoking the value change callback.</summary>
  public void UpdateValuesWithoutNotify(float value, float maxValue) {
    _sliderInitialization = true;
    _slider.UpdateValuesWithoutNotify(value, maxValue);
    _sliderInitialization = false;
  }

  void OnWaterLevelSliderChanged(float newValue) {
    var step = 1f / _stepSize;
    var adjustedValue = (float)Math.Round(newValue * step) / step;
    _slider.SetValueWithoutNotify(adjustedValue);
    if (!_sliderInitialization) {
      _onValueChangedFn(adjustedValue);
    }
  }
}
