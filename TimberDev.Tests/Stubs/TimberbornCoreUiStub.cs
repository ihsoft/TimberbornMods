using System;
using System.Collections.Generic;
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

public interface IPanelController {
  VisualElement GetPanel();
  bool OnUIConfirmed();
  void OnUICancelled();
}

public sealed class PanelStack {
  public readonly List<IPanelController> Pushed = new();
  public readonly List<IPanelController> Popped = new();

  public void PushDialog(IPanelController controller) {
    Pushed.Add(controller);
  }

  public void Pop(IPanelController controller) {
    Popped.Add(controller);
  }
}

public sealed class DialogBoxShower {
  public DialogBox LastDialogBox { get; private set; }

  public DialogBox Create() {
    LastDialogBox = new DialogBox();
    return LastDialogBox;
  }
}

public sealed class DialogBox {
  public string Message { get; private set; }
  public Action ConfirmAction { get; private set; }
  public Action CancelAction { get; private set; }
  public bool Shown { get; private set; }

  public DialogBox SetMessage(string message) {
    Message = message;
    return this;
  }

  public DialogBox SetConfirmButton(Action action) {
    ConfirmAction = action;
    return this;
  }

  public DialogBox SetCancelButton(Action action) {
    CancelAction = action;
    return this;
  }

  public void Show() {
    Shown = true;
  }
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
