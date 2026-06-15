using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class VisualElement {
  readonly List<VisualElement> _children = new();
  readonly HashSet<string> _classes = new();
  readonly Dictionary<Type, List<Delegate>> _callbacks = new();
  readonly Dictionary<Type, List<Delegate>> _callbacksOnce = new();

  public string name;
  public VisualElementStyle style { get; set; } = new();
  public ResolvedStyle resolvedStyle { get; set; } = new();
  public VisualElement parent { get; private set; }
  public IVisualElementScheduler schedule { get; } = new TestScheduler();
  public int childCount => _children.Count;
  public bool enabledSelf { get; private set; } = true;
  public StyleSheetList styleSheets { get; } = new();

  public void Add(VisualElement child) {
    child.parent = this;
    _children.Add(child);
  }

  public void Insert(int index, VisualElement child) {
    child.parent = this;
    _children.Insert(index, child);
  }

  public int IndexOf(VisualElement child) {
    return _children.IndexOf(child);
  }

  public void Clear() {
    foreach (var child in _children) {
      child.parent = null;
    }
    _children.Clear();
  }

  public VisualElement ChildAt(int index) {
    return _children[index];
  }

  public T Q<T>(string queryName = null) where T : VisualElement {
    if (this is T self && queryName == null) {
      return self;
    }
    foreach (var child in _children) {
      if (child is T typed && (queryName == null || child.name == queryName)) {
        return typed;
      }
      var nested = child.Q<T>(queryName);
      if (nested != null) {
        return nested;
      }
    }
    return null;
  }

  public VisualElement Q(string queryName) {
    return Q<VisualElement>(queryName);
  }

  public void AddToClassList(string className) {
    _classes.Add(className);
  }

  public void EnableInClassList(string className, bool enabled) {
    if (enabled) {
      AddToClassList(className);
    } else {
      RemoveFromClassList(className);
    }
  }

  public void RemoveFromClassList(string className) {
    _classes.Remove(className);
  }

  public bool ClassListContains(string className) {
    return _classes.Contains(className);
  }

  public void SetEnabled(bool value) {
    enabledSelf = value;
  }

  public void RegisterCallback<TEvent>(Action<TEvent> callback) {
    AddCallback(_callbacks, callback);
  }

  public void RegisterCallbackOnce<TEvent>(Action<TEvent> callback) {
    AddCallback(_callbacksOnce, callback);
  }

  public void TriggerEvent<TEvent>(TEvent evt) {
    InvokeCallbacks(_callbacks, evt);
    InvokeCallbacks(_callbacksOnce, evt);
    _callbacksOnce.Remove(typeof(TEvent));
  }

  static void AddCallback<TEvent>(Dictionary<Type, List<Delegate>> callbacks, Action<TEvent> callback) {
    var type = typeof(TEvent);
    if (!callbacks.TryGetValue(type, out var list)) {
      list = new List<Delegate>();
      callbacks[type] = list;
    }
    list.Add(callback);
  }

  static void InvokeCallbacks<TEvent>(Dictionary<Type, List<Delegate>> callbacks, TEvent evt) {
    if (!callbacks.TryGetValue(typeof(TEvent), out var list)) {
      return;
    }
    foreach (var callback in list.ToArray()) {
      ((Action<TEvent>)callback)(evt);
    }
  }
}

public class Button : VisualElement {
  public string text;
  public event Action clicked;

  public void Click() {
    clicked?.Invoke();
    TriggerEvent(new ClickEvent());
  }
}

public class TextField : VisualElement {
}

public class Toggle : VisualElement {
  Action<ChangeEvent<bool>> _valueChangedCallback;

  public string text;
  public bool value { get; private set; }

  public void RegisterValueChangedCallback(Action<ChangeEvent<bool>> callback) {
    _valueChangedCallback = callback;
  }

  public void SetValueWithoutNotify(bool newValue) {
    value = newValue;
  }

  public void TriggerValueChanged(bool previousValue, bool newValue) {
    value = newValue;
    _valueChangedCallback?.Invoke(ChangeEvent<bool>.GetPooled(previousValue, newValue));
  }
}

public class Slider : VisualElement {
  Action<ChangeEvent<float>> _valueChangedCallback;

  public float value;
  public float lowValue;
  public float highValue;

  public Slider(float lowValue, float highValue) {
    this.lowValue = lowValue;
    this.highValue = highValue;
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<float>> callback) {
    _valueChangedCallback = callback;
  }

  public void SetValueWithoutNotify(float newValue) {
    value = newValue;
  }

  public void TriggerValueChanged(float previousValue, float newValue) {
    value = newValue;
    _valueChangedCallback?.Invoke(ChangeEvent<float>.GetPooled(previousValue, newValue));
  }
}

public class MinMaxSlider : VisualElement {
  Action<ChangeEvent<UnityEngine.Vector2>> _valueChangedCallback;

  public UnityEngine.Vector2 value;

  public MinMaxSlider(float minValue, float maxValue, float lowLimit, float highLimit) {
    value = new UnityEngine.Vector2(lowLimit, highLimit);
  }

  public void RegisterValueChangedCallback(Action<ChangeEvent<UnityEngine.Vector2>> callback) {
    _valueChangedCallback = callback;
  }

  public void SetValueWithoutNotify(UnityEngine.Vector2 newValue) {
    value = newValue;
  }

  public void TriggerValueChanged(UnityEngine.Vector2 previousValue, UnityEngine.Vector2 newValue) {
    value = newValue;
    _valueChangedCallback?.Invoke(ChangeEvent<UnityEngine.Vector2>.GetPooled(previousValue, newValue));
  }
}

public class ScrollView : VisualElement {
}

public class Label : VisualElement {
  public string text;
}

public class Image : VisualElement {
  public UnityEngine.Sprite sprite;
}

public class VisualTreeAsset {
  public void CloneTree(VisualElement target) {
    target.Add(new Label { name = "Label" });
    target.Add(new VisualElement { name = "SelectedItemContent" });
    target.Add(new Button { name = "Selection" });
    target.Add(new Button { name = "ArrowLeft" });
    target.Add(new Button { name = "ArrowRight" });
    target.Add(new Button { name = "ConfirmButton" });
    target.Add(new Button { name = "CancelButton" });
    target.Add(new Button { name = "CloseButton" });
  }

  public VisualElement Instantiate() {
    var root = new VisualElement();
    CloneTree(root);
    return root;
  }
}

public class ClickEvent {
}

public class DetachFromPanelEvent {
}

public class GeometryChangedEvent {
}

public sealed class VisualElementStyle {
  public StyleLength width;
  public int paddingRight;
  public int marginBottom;
  public int paddingTop;
  public int paddingLeft;
  public int paddingBottom;
  public int paddingRightButton;
  public Justify justifyContent;
  public FlexDirection flexDirection;
}

public sealed class ResolvedStyle {
  public float width;
}

public readonly struct StyleLength {
  public readonly float Value;
  public readonly StyleKeyword Keyword;

  public StyleLength(float value) {
    Value = value;
    Keyword = StyleKeyword.Undefined;
  }

  public StyleLength(StyleKeyword keyword) {
    Value = 0;
    Keyword = keyword;
  }

  public static implicit operator StyleLength(float value) {
    return new StyleLength(value);
  }
}

public enum StyleKeyword {
  Undefined,
  Null,
}

public enum Justify {
  FlexStart,
  Center,
}

public enum FlexDirection {
  Column,
  Row,
}

public class StyleSheet {
}

public sealed class StyleSheetList : List<StyleSheet> {
}

public sealed class ChangeEvent<T> {
  public T previousValue;
  public T newValue;

  public static ChangeEvent<T> GetPooled(T previousValue, T newValue) {
    return new ChangeEvent<T> {
        previousValue = previousValue,
        newValue = newValue,
    };
  }
}

public static class UQueryExtensions {
  public static T Q<T>(VisualElement element, string name, string className = null) where T : VisualElement {
    return element.Q<T>(name);
  }
}

public interface IVisualElementScheduler {
  IVisualElementScheduledItem Execute(Action action);
}

public interface IVisualElementScheduledItem {
  IVisualElementScheduledItem StartingIn(long delayMs);
  IVisualElementScheduledItem Until(Func<bool> stopCondition);
}

public sealed class TestScheduler : IVisualElementScheduler {
  public readonly List<Action> Actions = new();

  public IVisualElementScheduledItem Execute(Action action) {
    Actions.Add(action);
    return new TestScheduledItem();
  }
}

public sealed class TestScheduledItem : IVisualElementScheduledItem {
  public IVisualElementScheduledItem StartingIn(long delayMs) {
    return this;
  }

  public IVisualElementScheduledItem Until(Func<bool> stopCondition) {
    return this;
  }
}
