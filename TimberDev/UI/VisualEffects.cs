// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Utility methods to create effect on VisualElement.</summary>
public static class VisualEffects {
  /// <summary>Schedules a delayed effect on the element.</summary>
  public static void ScheduleSwitchEffect<T, TV>(
      T element, int delayMs, TV startState, TV endState, Action<T, TV> action)
      where T : VisualElement {
    action(element, startState);
    element.schedule.Execute(() => action(element, endState)).StartingIn(delayMs).Until(() => true);
  }

  /// <summary>Applies a class to the element for a limited period of time.</summary>
  public static void SetTemporaryClass(VisualElement element, int delayMs, string className) {
    element.AddToClassList(className);
    element.schedule.Execute(() => element.RemoveFromClassList(className)).StartingIn(delayMs).Until(() => true);
  }
}
