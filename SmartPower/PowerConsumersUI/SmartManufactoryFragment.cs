// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.PowerConsumers;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerConsumersUI;

sealed class SmartManufactoryFragment : IEntityPanelFragment {

  readonly ConsumerFragmentPatcher _consumerFragmentPatcher;

  VisualElement _root;
  SmartManufactory _smartManufactory;

  SmartManufactoryFragment(ConsumerFragmentPatcher consumerFragmentPatcher) {
    _consumerFragmentPatcher = consumerFragmentPatcher;
  }

  public VisualElement InitializeFragment() {
    _root = new VisualElement();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _consumerFragmentPatcher.InitializePatch(_root);
    _smartManufactory = entity.GetComponent<SmartManufactory>();
  }

  public void ClearFragment() {
    _consumerFragmentPatcher.HideAllElements();
    _smartManufactory = null;
  }

  public void UpdateFragment() {
    if (_smartManufactory) {
      _consumerFragmentPatcher.UpdateSmartManufactory(_smartManufactory);
    }
  }
}
