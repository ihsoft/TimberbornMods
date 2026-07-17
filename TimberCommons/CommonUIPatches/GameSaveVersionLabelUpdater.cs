// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.VersioningSerialization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;
using Version = Timberborn.Versioning.Version;

namespace IgorZ.TimberCommons.CommonUIPatches;

sealed class GameSaveVersionLabelUpdater : ILoadableSingleton {
  readonly GameSaveDeserializer _gameSaveDeserializer;
  readonly FactionSpecService _factionSpecService;
  readonly LoadGameBox _loadGameBox;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly VersionSerializer _versionSerializer;
  readonly FactionIdSaveEntryReader _factionIdSaveEntryReader = new();
  VisualElement _factionIndicator;
  Label _factionLabel;
  string _factionTooltip = "";
  Button _saveVersionLabel;

  static GameSaveVersionLabelUpdater _instance;

  public GameSaveVersionLabelUpdater(
      GameSaveDeserializer gameSaveDeserializer, FactionSpecService factionSpecService, LoadGameBox loadGameBox,
      ITooltipRegistrar tooltipRegistrar, VersionSerializer versionSerializer) {
    _gameSaveDeserializer = gameSaveDeserializer;
    _factionSpecService = factionSpecService;
    _loadGameBox = loadGameBox;
    _tooltipRegistrar = tooltipRegistrar;
    _versionSerializer = versionSerializer;
    _instance = this;
  }

  internal static void UpdateCurrent() {
    _instance?.Update();
  }

  public void Load() {
    var showSavedMods = _loadGameBox._showSavedMods;
    if (showSavedMods?.parent == null) {
      DebugEx.Warning("Cannot add save version label: ShowSavedModsButton is missing or has no parent");
      _instance = null;
      return;
    }

    var saveInfoLabels = new VisualElement();
    saveInfoLabels.style.position = Position.Absolute;
    saveInfoLabels.style.right = 10;
    saveInfoLabels.style.bottom = 10;
    saveInfoLabels.style.flexDirection = FlexDirection.Row;
    showSavedMods.parent.Add(saveInfoLabels);

    _factionIndicator = new VisualElement {
        pickingMode = PickingMode.Position,
    };
    _factionIndicator.style.position = Position.Absolute;
    _factionIndicator.style.left = 10;
    _factionIndicator.style.top = 10;
    _factionIndicator.style.alignItems = Align.Center;
    _factionIndicator.style.justifyContent = Justify.Center;
    _factionIndicator.ToggleDisplayStyle(visible: false);
    _factionLabel = new Label();
    foreach (var className in showSavedMods.GetClasses()) {
      _factionLabel.AddToClassList(className);
    }
    _factionLabel.style.paddingLeft = 8;
    _factionLabel.style.paddingRight = 8;
    _factionLabel.style.paddingTop = 3;
    _factionLabel.style.paddingBottom = 3;
    _factionIndicator.Add(_factionLabel);
    _tooltipRegistrar.Register(_factionIndicator, () => _factionTooltip);
    showSavedMods.parent.Add(_factionIndicator);

    _saveVersionLabel = new Button {
        focusable = false,
        pickingMode = PickingMode.Ignore,
    };
    foreach (var className in showSavedMods.GetClasses()) {
      _saveVersionLabel.AddToClassList(className);
    }
    _saveVersionLabel.style.position = Position.Relative;
    _saveVersionLabel.style.right = StyleKeyword.Auto;
    _saveVersionLabel.style.bottom = StyleKeyword.Auto;
    _saveVersionLabel.style.flexShrink = 0;
    _saveVersionLabel.style.marginRight = 6;
    _saveVersionLabel.ToggleDisplayStyle(visible: false);
    saveInfoLabels.Add(_saveVersionLabel);

    showSavedMods.RemoveFromHierarchy();
    showSavedMods.style.position = Position.Relative;
    showSavedMods.style.right = StyleKeyword.Auto;
    showSavedMods.style.bottom = StyleKeyword.Auto;
    saveInfoLabels.Add(showSavedMods);
  }

  void Update() {
    if (_saveVersionLabel == null) {
      return;
    }

    _saveVersionLabel.ToggleDisplayStyle(visible: false);
    _factionIndicator.ToggleDisplayStyle(visible: false);
    if (!_loadGameBox._saveList.TryGetSelectedSave(out var selectedSave)) {
      return;
    }

    UpdateFaction(selectedSave);
    var saveVersion = GetSaveVersion(selectedSave);
    if (saveVersion == null) {
      return;
    }

    _saveVersionLabel.text = saveVersion;
    _saveVersionLabel.ToggleDisplayStyle(visible: true);
  }

  void UpdateFaction(GameSaveItem selectedSave) {
    try {
      var factionId = _gameSaveDeserializer.ReadFromSaveFileUnsafe(
          selectedSave.SaveReference, _factionIdSaveEntryReader);
      var faction = _factionSpecService.Factions.FirstOrDefault(candidate => candidate.Id == factionId);
      var logo = faction?.Logo.Asset;
      _factionTooltip = faction?.DisplayName.Value ?? factionId;
      _factionLabel.ToggleDisplayStyle(logo == null);
      _factionLabel.text = logo == null ? factionId : "";
      _factionIndicator.style.width = logo == null ? StyleKeyword.Auto : 48;
      _factionIndicator.style.height = logo == null ? StyleKeyword.Auto : 48;
      _factionIndicator.style.backgroundColor = logo == null ? new Color(0, 0, 0, 0.65f) : Color.clear;
      _factionIndicator.style.backgroundImage = logo == null ? StyleKeyword.None : new StyleBackground(logo);
      _factionIndicator.ToggleDisplayStyle(visible: true);
    } catch (Exception e) {
      DebugEx.Warning("Failed to read faction from {0}: {1}", selectedSave.DisplayName, e);
    }
  }

  string GetSaveVersion(GameSaveItem selectedSave) {
    try {
      var version = _gameSaveDeserializer.ReadFromSaveFileUnsafe(selectedSave.SaveReference, _versionSerializer);
      return FormatVersion(version);
    } catch (Exception e) {
      DebugEx.Warning("Failed to read save version from {0}: {1}", selectedSave.DisplayName, e);
      return null;
    }
  }

  static string FormatVersion(Version version) {
    var versionParts = version.Numeric.Split('.');
    var visibleParts = Math.Min(versionParts.Length, 4);
    while (visibleParts > 2 && versionParts[visibleParts - 1] == "0") {
      visibleParts--;
    }
    return "v" + string.Join(".", versionParts, 0, visibleParts);
  }
}
