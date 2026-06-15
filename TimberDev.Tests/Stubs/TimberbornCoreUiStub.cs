using System;
using UnityEngine.UIElements;

namespace Timberborn.CoreUI;

public sealed class PreciseSlider : VisualElement {
  Action<float> _valueChangedCallback;

  public float Value { get; private set; }
  public float MaxValue { get; private set; }
  public float Step { get; private set; }

  public void SetStepWithoutNotify(float step) {
    Step = step;
  }

  public void SetValueChangedCallback(Action<float> valueChangedCallback) {
    _valueChangedCallback = valueChangedCallback;
  }

  public void SetValueWithoutNotify(float value) {
    Value = value;
  }

  public void UpdateValuesWithoutNotify(float value, float maxValue) {
    Value = value;
    MaxValue = maxValue;
  }

  public void TriggerValueChanged(float value) {
    _valueChangedCallback(value);
  }
}
