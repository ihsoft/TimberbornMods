// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Simple dropdown field with a value and an arbitrary text. Icons are not supported.</summary>
/// <example>
/// <code><![CDATA[
/// var dropdown1 = UiBuilder.Create<ResizableDropdown>().BuildAndInitialize();
/// var dropdown2 = Uifactory.CreateSimpleDropdown(v => SetValue(v));
/// ]]></code>
/// </example>
public sealed class ResizableDropdownElement : VisualElement {

  const string SelectableClass = "dropdown__selectable";
  const string ItemSelectedClass = "dropdown-item--selected";

  #region API

  /// <summary>Event that is triggered when the value of the dropdown is selected.</summary>
  /// <remarks>It triggers even if the selected value hasn't changed.</remarks>
  public event EventHandler OnValueChanged;

  /// <summary>Whether the dropdown should resize to fit the options.</summary>
  /// <remarks>
  /// Each time the options set is changed, the control will resize itself. Set min/max width via the style to limit the
  /// possible control sizes. If the option text can't fit the control, it will be truncated with ellipsis.
  /// </remarks>
  public bool AutoResizeToOptions {
    get => _autoResizeToOptions;
    set {
      _autoResizeToOptions = value;
      if (value) {
        ResizeWidth();
      } else {
        _selectedItem.style.width = new StyleLength(StyleKeyword.Null);
      }
    }
  }
  bool _autoResizeToOptions = true;

  /// <summary>The items to display in the dropdown.</summary>
  /// <remarks>
  /// Changing this set will reset <see cref="SelectedValue"/> to the first item. The callback will fire.
  /// </remarks>
  public DropdownItem<string>[] Items {
    get => _items;
    set => SetItems(value);
  }
  DropdownItem<string>[] _items;

  /// <summary>The currently selected value of the dropdown.</summary>
  public string SelectedValue {
    get => _selectedValue;
    set {
      _selectedValue = value;
      UpdateSelectedValue();
      _dropdownListDrawer.HideDropdown();
      OnValueChanged?.Invoke(this, EventArgs.Empty);
    }
  }
  string _selectedValue;

  /// <summary>The text label of the dropdown.</summary>
  /// <remarks>It is hidden by default.</remarks>
  public Label TextLabel => this.Q<Label>("Label");

  #endregion

  #region Implemenation

  DropdownListDrawer _dropdownListDrawer;
  VisualElementLoader _visualElementLoader;

  Button _selection;
  VisualElement _selectedItem;
  readonly List<VisualElement> _elements = [];

  /// <inheritdoc cref="ResizableDropdownElement"/>
  public ResizableDropdownElement() {
    // ReSharper disable once Unity.UnknownResource
    Resources.Load<VisualTreeAsset>("UI/Views/Core/Dropdown").CloneTree(this);
    this.Q<Label>("Label").ToggleDisplayStyle(false);
  }

  internal void Initialize(DropdownListDrawer dropdownListDrawer, VisualElementLoader visualElementLoader) {
    _dropdownListDrawer = dropdownListDrawer;
    _visualElementLoader = visualElementLoader;

    _selectedItem = this.Q<VisualElement>("SelectedItemContent");
    _selection = this.Q<Button>("Selection");
    _selection.EnableInClassList(SelectableClass, true);
    _selection.RegisterCallback<DetachFromPanelEvent>(delegate {
      _dropdownListDrawer.HideDropdown();
    });
    this.Q<Button>("ArrowLeft").ToggleDisplayStyle(visible: false);
    this.Q<Button>("ArrowRight").ToggleDisplayStyle(visible: false);
    _selection.RegisterCallback<ClickEvent>(ToggleSelectionListDisplayStyle);
  }

  void SetItems(DropdownItem<string>[] items) {
    _elements.Clear();
    _items = items;
    SelectedValue = _items.FirstOrDefault().Value;
    if (_autoResizeToOptions) {
      ResizeWidth();
    }
  }

  void UpdateSelectedValue() {
    if (_resizeScheduled) {
      return;
    }
    _selectedItem.Clear();
    var option = _items.FirstOrDefault(x => x.Value == _selectedValue);
    _selectedItem.Add(CreateSelectedItemElement(option.Text));
  }

  void ToggleSelectionListDisplayStyle(ClickEvent evt) {
    if (_dropdownListDrawer.DropdownVisible) {
      _dropdownListDrawer.HideDropdown();
      return;
    }
    _dropdownListDrawer.ShowDropdown(_selection, _elements);
  }

  void ResizeWidth() {
    _selectedItem.Clear();
    foreach (var option in _items) {
      var element = CreateItemElement(option.Text);
      element.RegisterCallback<ClickEvent>(_ => SelectedValue = option.Value);
      _elements.Add(element);
      _selectedItem.Add(CreateSelectedItemElement(option.Text));
    }
    if (_resizeScheduled) {
      return;
    }
    _resizeScheduled = true;
    _selectedItem.style.width = new StyleLength(StyleKeyword.Null);
    RegisterCallbackOnce<GeometryChangedEvent>(_ => {
      _resizeScheduled = false;
      _selectedItem.style.width = _selectedItem.resolvedStyle.width;
      UpdateSelectedValue();
    });
  }
  bool _resizeScheduled;

  VisualElement CreateItemElement(string text) {
    var visualElement = _visualElementLoader.LoadVisualElement("Core/DropdownItem");
    visualElement.Q("Icon").ToggleDisplayStyle(false);
    var textElement = visualElement.Q<Label>("Text");
    textElement.text = text;
    var container = textElement.parent;
    container.style.paddingRight = 0;

    return visualElement;
  }

  VisualElement CreateSelectedItemElement(string text) {
    var item = CreateItemElement(text);
    item.SetEnabled(value: false);
    item.AddToClassList(ItemSelectedClass);
    return item;
  }

  #endregion
}
