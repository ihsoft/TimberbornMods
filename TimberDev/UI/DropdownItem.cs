// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace

using UnityEngine;

namespace IgorZ.TimberDev.UI;

/// <summary>Dropdown item with a value and a localized text.</summary>
public record struct DropdownItem {
  /// <summary>The value of the dropdown item.</summary>
  public string Value { get; init; }

  /// <summary>The text to display in the dropdown.</summary>
  public string Text { get; init; }

  /// <summary>Optional icon to show before the text.</summary>
  public Sprite Icon { get; init; }

  /// <summary>Implicit conversion from a tuple to the DropdownItem.</summary>
  public static implicit operator DropdownItem((string value, string text) tuple) {
    return new DropdownItem { Value = tuple.value, Text = tuple.text };
  }

  /// <summary>Implicit conversion from a tuple to the DropdownItem.</summary>
  public static implicit operator DropdownItem<T>((T value, string text) tuple) {
    return new DropdownItem<T> { Value = tuple.value, Text = tuple.text };
  }
}
