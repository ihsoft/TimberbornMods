using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Timberborn.DropdownSystem;

public sealed class DropdownListDrawer {
  public bool DropdownVisible { get; private set; }
  public VisualElement LastAnchor { get; private set; }
  public List<VisualElement> LastElements { get; private set; }

  public void ShowDropdown(VisualElement anchor, List<VisualElement> elements) {
    DropdownVisible = true;
    LastAnchor = anchor;
    LastElements = elements;
  }

  public void HideDropdown() {
    DropdownVisible = false;
  }
}
