// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Dropdown item with a value and a localized text.</summary>
public struct DropdownItem<T> where T : notnull {
  /// <summary>The value of the dropdown item.</summary>
  public T Value;

  /// <summary>The text to display in the dropdown.</summary>
  public string Text;

  /// <summary>Implicit conversion from a tuple to the DropdownItem.</summary>
  public static implicit operator DropdownItem<T>((T value, string text) tuple) {
    return new DropdownItem<T> { Value = tuple.value, Text = tuple.text };
  }
}
