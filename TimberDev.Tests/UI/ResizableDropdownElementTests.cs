using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class ResizableDropdownElementTests {
  public static void SelectsFirstItemAndUpdatesValue() {
    var dropdownListDrawer = new DropdownListDrawer();
    var dropdown = CreateDropdown(dropdownListDrawer);
    var changed = 0;
    dropdown.OnValueChanged += (_, _) => changed++;
    var icon = new Sprite();

    dropdown.Items = [
        ("first", "First"),
        ("second", icon, "Second"),
    ];

    Assert.Equal("first", dropdown.SelectedValue);
    Assert.Equal(1, changed);

    dropdown.Q<Button>("Selection").Click();
    Assert.True(dropdownListDrawer.DropdownVisible);
    Assert.Equal(2, dropdownListDrawer.LastElements.Count);

    dropdownListDrawer.LastElements[1].TriggerEvent(new ClickEvent());

    Assert.Equal("second", dropdown.SelectedValue);
    Assert.False(dropdownListDrawer.DropdownVisible);
    Assert.Equal(2, changed);
  }

  public static void AutoResizeCanBeDisabled() {
    var dropdown = CreateDropdown(new DropdownListDrawer());

    dropdown.Items = [
        ("first", "First"),
        ("second", "Second"),
    ];
    var selectedItem = dropdown.Q<VisualElement>("SelectedItemContent");
    selectedItem.resolvedStyle.width = 123f;
    dropdown.TriggerEvent(new GeometryChangedEvent());

    Assert.Equal(123f, selectedItem.style.width.Value);

    dropdown.AutoResizeToOptions = false;

    Assert.Equal(StyleKeyword.Null, selectedItem.style.width.Keyword);
  }

  static ResizableDropdownElement CreateDropdown(DropdownListDrawer dropdownListDrawer) {
    var dropdown = new ResizableDropdownElement();
    dropdown.Initialize(dropdownListDrawer, new VisualElementLoader());
    return dropdown;
  }
}
