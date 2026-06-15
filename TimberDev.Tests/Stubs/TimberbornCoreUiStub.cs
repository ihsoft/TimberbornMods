using System;
using UnityEngine.UIElements;

namespace Timberborn.CoreUI;

public static class VisualElementDisplayExtensions {
  public static void ToggleDisplayStyle(this VisualElement element, bool visible) {
    element.EnableInClassList("displayed", visible);
  }
}

public sealed class VisualElementLoader {
  public VisualTreeAsset LoadVisualTreeAsset(string name) {
    return new VisualTreeAsset();
  }

  public VisualElement LoadVisualElement(string name) {
    var root = new VisualElement();
    var container = new VisualElement();
    root.Add(container);
    container.Add(new Image { name = "Icon" });
    container.Add(new Label { name = "Text" });
    return root;
  }
}

public sealed class VisualElementInitializer {
  public readonly System.Collections.Generic.List<VisualElement> Initialized = new();

  public void InitializeVisualElement(VisualElement element) {
    Initialized.Add(element);
    element.AddToClassList("initialized");
  }
}

public class NineSliceVisualElement : VisualElement {
}

public class NineSliceLabel : Label {
}

public class NineSliceButton : Button {
}

public class NineSliceTextField : TextField {
}

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
