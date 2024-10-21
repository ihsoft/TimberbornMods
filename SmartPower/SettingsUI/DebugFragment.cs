// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using ModSettings.CoreUI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.Debugging;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.Modding;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.SettingsUI;

sealed class DebugFragment : IEntityPanelFragment {
  readonly DevModeManager _devModeManager;
  readonly DebugFragmentFactory _debugFragmentFactory;
  readonly ModRepository _modRepository;
  readonly ModSettingsBox _modSettingsBox;

  DistrictCenter _districtCenter;
  VisualElement _root;

  DebugFragment(DevModeManager devModeManager,
                DebugFragmentFactory debugFragmentFactory,
                ModRepository modRepository,
                ModSettingsBox modSettingsBox) {
    _devModeManager = devModeManager;
    _debugFragmentFactory = debugFragmentFactory;
    _modRepository = modRepository;
    _modSettingsBox = modSettingsBox;
  }

  /// <inheritdoc/>
  public VisualElement InitializeFragment() {
    var debugFragmentButton = new DebugFragmentButton(
      () => {
        var mod = _modRepository.EnabledMods.First(m => m.Manifest.Id == Settings.Configurator.ModId);
        _modSettingsBox.Open(mod);
      }, "SmartPower Settings");
    _root = _debugFragmentFactory.Create("SmartPower", debugFragmentButton);
    _root.Q<VisualElement>("TitleWrapper").ToggleDisplayStyle(visible: false);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _districtCenter = entity.GetComponentFast<DistrictCenter>();
  }

  /// <inheritdoc/>
  public void ClearFragment() {
    _districtCenter = null;
    UpdateFragment();
  }

  /// <inheritdoc/>
  public void UpdateFragment() {
    _root.ToggleDisplayStyle(_districtCenter && _districtCenter.enabled && _devModeManager.Enabled);
  }
}
