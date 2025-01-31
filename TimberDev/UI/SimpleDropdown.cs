// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using TimberApi.UIPresets.Dropdowns;
using Timberborn.DropdownSystem;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Simple dropdown wrapper that allows to interact with the game dropdown element.</summary>
public sealed class SimpleDropdown<T>  where T : notnull {

  /// <summary>A wrapper to interact with the game's dropdown element.</summary>
  sealed class DropdownProvider(SimpleDropdown<T> dropdown) : IExtendedDropdownProvider {
    public IReadOnlyList<string> Items => dropdown.Items.Select(x => x.Value.ToString()).ToList();
    public string GetValue() => dropdown.StringValue;
    public string FormatDisplayText(string value) =>
        dropdown.Items.FirstOrDefault(x => x.Value.ToString() == value).Text;
    public Sprite GetIcon(string value) => dropdown.Items.FirstOrDefault(x => x.Value.ToString() == value).Icon;

    public void SetValue(string value) {
      if (value == dropdown.StringValue) {
        return;
      }
      dropdown._value = dropdown.Items.FirstOrDefault(x => x.Value.ToString() == value).Value;
      dropdown._onValueChanged?.Invoke(dropdown.Value);
    }
  }

  /// <summary>The game's original dropdown element. Add it to the UI as a VisualElement.</summary>
  public Dropdown DropdownElement { get; }

  /// <summary>The currently selected value of the dropdown.</summary>
  public T Value {
    get => _value;
    set {
      _value = value;
      _onValueChanged?.Invoke(_value);
      DropdownElement.UpdateSelectedValue(StringValue);
    }
  }
  T _value;

  string StringValue => _value != null ? _value.ToString() : "";

  /// <summary>The list of items to display in the dropdown.</summary>
  public DropdownItem<T>[] Items {
    get => _items;
    set {
      _items = value;
      _value = _items.FirstOrDefault().Value;
      _dropdownItemsSetter.SetItems(DropdownElement, _dropdownProvider);
      _onValueChanged?.Invoke(_value);
    }
  }
  DropdownItem<T>[] _items;

  readonly DropdownItemsSetter _dropdownItemsSetter;
  readonly Action<T> _onValueChanged;
  readonly DropdownProvider _dropdownProvider;

  /// <summary>Creates a new instance of the SimpleDropdown.</summary>
  /// <param name="uiFactory"></param>
  /// <param name="dropdownItemsSetter"></param>
  /// <param name="onValueChanged"></param>
  public SimpleDropdown(
      UiFactory uiFactory, DropdownItemsSetter dropdownItemsSetter, Action<T> onValueChanged) {
    _dropdownItemsSetter = dropdownItemsSetter;
    _onValueChanged = onValueChanged;
    _dropdownProvider = new DropdownProvider(this);
    DropdownElement = uiFactory.UiBuilder.Create<GameDropdown>().SetName("").BuildAndInitialize();
  }
}
