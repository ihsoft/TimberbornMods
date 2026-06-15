using IgorZ.TimberDev.UI;
using UnityEngine;

namespace TimberDev.Tests;

static class DropdownItemTests {
  public static void ConvertsFromTuples() {
    DropdownItem textOnly = ("value", "Text");

    Assert.Equal("value", textOnly.Value);
    Assert.Equal("Text", textOnly.Text);
    Assert.Equal(null, textOnly.Icon);

    var icon = new Sprite();
    DropdownItem withIcon = ("value2", icon, "Text2");

    Assert.Equal("value2", withIcon.Value);
    Assert.Equal("Text2", withIcon.Text);
    Assert.Equal(icon, withIcon.Icon);
  }
}
