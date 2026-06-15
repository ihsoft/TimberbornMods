using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements;

public class VisualElement {
  readonly List<VisualElement> _children = new();
  readonly HashSet<string> _classes = new();

  public string name;
  public VisualElement parent { get; private set; }
  public IVisualElementScheduler schedule { get; } = new TestScheduler();
  public int childCount => _children.Count;

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

  public VisualElement ChildAt(int index) {
    return _children[index];
  }

  public T Q<T>(string queryName) where T : VisualElement {
    foreach (var child in _children) {
      if (child is T typed && child.name == queryName) {
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

  public void RemoveFromClassList(string className) {
    _classes.Remove(className);
  }

  public bool ClassListContains(string className) {
    return _classes.Contains(className);
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
