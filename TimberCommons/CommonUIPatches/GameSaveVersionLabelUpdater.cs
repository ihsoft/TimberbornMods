// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.SingletonSystem;
using Timberborn.VersioningSerialization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;
using Version = Timberborn.Versioning.Version;

namespace IgorZ.TimberCommons.CommonUIPatches;

sealed class GameSaveVersionLabelUpdater : ILoadableSingleton {
  readonly GameSaveDeserializer _gameSaveDeserializer;
  readonly LoadGameBox _loadGameBox;
  readonly VersionSerializer _versionSerializer;
  Button _saveVersionLabel;

  static GameSaveVersionLabelUpdater _instance;

  public GameSaveVersionLabelUpdater(
      GameSaveDeserializer gameSaveDeserializer, LoadGameBox loadGameBox, VersionSerializer versionSerializer) {
    _gameSaveDeserializer = gameSaveDeserializer;
    _loadGameBox = loadGameBox;
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
    if (!_loadGameBox._saveList.TryGetSelectedSave(out var selectedSave)) {
      return;
    }

    var saveVersion = GetSaveVersion(selectedSave);
    if (saveVersion == null) {
      return;
    }

    _saveVersionLabel.text = saveVersion;
    _saveVersionLabel.ToggleDisplayStyle(visible: true);
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
