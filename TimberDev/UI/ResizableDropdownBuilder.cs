// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.UIBuilderSystem;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;

namespace IgorZ.TimberDev.UI;

#pragma warning disable CS1591

public class ResizableDropdown : ResizableDropdown<ResizableDropdown> {
  protected override ResizableDropdown BuilderInstance => this;
}

public abstract class ResizableDropdown<TBuilder>
    : BaseBuilder<TBuilder, ResizableDropdownElement> where TBuilder : BaseBuilder<TBuilder, ResizableDropdownElement> {

  VisualElementLoader _visualElementLoader;
  DropdownListDrawer _dropdownListDrawer;

  [Inject]
  public void InjectDependencies(DropdownListDrawer dropdownListDrawer, VisualElementLoader visualElementLoader) {
    _dropdownListDrawer = dropdownListDrawer;
    _visualElementLoader = visualElementLoader;
  }

  protected override ResizableDropdownElement InitializeRoot() {
    var element = new ResizableDropdownElement();
    element.Initialize(_dropdownListDrawer, _visualElementLoader);
    return element;
  }
}
