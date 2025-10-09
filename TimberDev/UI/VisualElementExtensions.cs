// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.TimberDev.UI;

/// <summary>Extensions for <see cref="VisualElement"/>.</summary>
public static class VisualElementExtensions {
  /// <summary>
  /// Like <see cref="UQueryExtensions.Q{T}(VisualElement, string, string)"/>, but reports the missing element.
  /// </summary>
  /// <typeparam name="T">The type of the element to find.</typeparam>
  /// <param name="e">The element to search in.</param>
  /// <param name="name">The name of the element to find.</param>
  /// <param name="throwIfNotFound">
  /// If <c>true</c> then the code will throw if the element is not found. Otherwise, it will log an error and return
  /// <c>null</c>.
  /// </param>
  /// <returns>The found element or <c>null</c> if not found and <c>throwIfNotFound</c> is <c>false</c>.</returns>
  /// <exception cref="InvalidOperationException">
  /// Thrown if the element is not found and <c>throwIfNotFound</c> is <c>true</c>.
  /// </exception>
  public static T Q2<T>(this VisualElement e, string name, bool throwIfNotFound = true) where T : VisualElement {
    var res = e.Q<T>(name);
    if (res != null) {
      return res;
    }
    DebugEx.Error("Element not found: {0}", name);
    return throwIfNotFound ? throw new InvalidOperationException($"Element not found: {name}") : null;
  }
}
