// Timberborn Mod: TimberUI
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.IO;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEditor.StyleSheets;
using UnityEngine.UIElements;

namespace IgorZ.TimberUI;

sealed class MainDialog : IPanelController, ILoadableSingleton {

  readonly PanelStack _panelStack;
  readonly VisualElementLoader _visualElementLoader;
  readonly IAssetLoader _assetLoader;
  readonly UiFactory _uiFactory;
  readonly TimberUISettings _settings;

  Label _logTextLabel;
  Label _errorText;
  TextField _resourcePathTextField;
  TextField _filenameTextField;

  VisualElement _root;

  MainDialog(PanelStack panelStack, VisualElementLoader visualElementLoader, IAssetLoader assetLoader,
             UiFactory uiFactory, TimberUISettings settings) {
    _panelStack = panelStack;
    _visualElementLoader = visualElementLoader;
    _assetLoader = assetLoader;
    _uiFactory = uiFactory;
    _settings = settings;
  }

  public VisualElement GetPanel() {
    return _root;
  }

  public bool OnUIConfirmed() {
    return true;
  }

  public void OnUICancelled() {
  }

  VisualElement GetDialogBox() {
    var dialogBox = _visualElementLoader.LoadVisualElement("Options/SettingsBox");
    dialogBox.Q<Label>("DeveloperTestLabel").ToggleDisplayStyle(false);
    dialogBox.Q<VisualElement>("Developer").ToggleDisplayStyle(false);
    dialogBox.Q<ScrollView>("Content").Clear();
    return dialogBox;
  }

  public void Load() {
    var panel = _uiFactory.LoadTimberDevElement("IgorZ/TimberUI-Dialog");
    _resourcePathTextField = panel.Q2<TextField>("ResourcePath");
    _filenameTextField = panel.Q2<TextField>("Filename");
    _filenameTextField.text = _settings.TargetPath.Value;
    panel.Q2<Button>("ExportUSSButton").clicked += ExportUss;
    panel.Q2<Button>("ExportUXMLButton").clicked += ExportUxml;
    panel.Q2<Button>("PrintStylesButton").clicked += PrintStyles;
    _logTextLabel = panel.Q2<Label>("LogText");
    _logTextLabel.ToggleDisplayStyle(false);
    _errorText = panel.Q2<Label>("ErrorText");
    _errorText.ToggleDisplayStyle(false);

    _root = GetDialogBox();
    _root.style.position = Position.Absolute;
    _root.Q2<ScrollView>("Content").Add(panel);
    _panelStack._root.Add(_root);
  }

  void ExportUss() {
    _errorText.ToggleDisplayStyle(false);
    _logTextLabel.ToggleDisplayStyle(false);
    var fullPath = _filenameTextField.text;
    if (!fullPath.EndsWith(".uss")) {
      fullPath = Path.Combine(fullPath, _resourcePathTextField.text + ".uss");
    }
    var error = TryLoadAsset<StyleSheet>(_resourcePathTextField.text, out var style)
        ?? VerifyTargetPath(fullPath);
    if (error != null) {
      _errorText.text = error;
      _errorText.ToggleDisplayStyle(true);
      return;
    }
    StyleSheetToUss.WriteStyleSheet(style, fullPath);
    _logTextLabel.text = $"Successfully exported {style.name} to {fullPath}";
    _logTextLabel.ToggleDisplayStyle(true);
  }

  string VerifyTargetPath(string path) {
    string error = null;
    if (string.IsNullOrEmpty(path)) {
      error = "Target path is empty";
    } else if (!Directory.Exists(Path.GetDirectoryName(path))) {
      error = $"Directory {Path.GetDirectoryName(path)} does not exist";
    }
    return error;
  }

  void ExportUxml() {
    DebugEx.Info("ExportUxml");
  }

  void PrintStyles() {
    VisualTreeAsset asset;
    _errorText.ToggleDisplayStyle(false);
    _logTextLabel.ToggleDisplayStyle(false);
    try {
      asset = _assetLoader.Load<VisualTreeAsset>(_resourcePathTextField.value);
    } catch (InvalidOperationException e) {
      _errorText.text = e.Message;
      _errorText.ToggleDisplayStyle(true);
      return;
    }
    var styles = asset.stylesheets.Select(x => x.name).ToList();
    _logTextLabel.text = "Styles:\n" + string.Join("\n", styles);
    _logTextLabel.ToggleDisplayStyle(true);
  }

  string TryLoadAsset<T>(string path, out T asset) where T : UnityEngine.Object {
    try {
      asset = _assetLoader.Load<T>(path);
      return null;
    } catch (Exception e) {
      asset = null;
      DebugEx.Error("Failed to load asset {0}: {1}", path, e);
      return $"Cannot load asset {path}:\n{e.Message}";
    }
  }

  static void DumpStyles(VisualElement element) {
    DebugEx.Info($"Element: {element}, styles");
    for (var i = 0; i < element.styleSheets.count; i++) {
      var sheet = element.styleSheets[i];
      DebugEx.Info("Element: {0}, style: {1}, importedWithErrors: {2}, importedWithWarnings: {3}",
                   element, sheet.name, sheet.importedWithErrors, sheet.importedWithWarnings);
    }
    foreach (var child in element.Children()) {
      DumpStyles(child);
    }
  }

  static void DumpStyles(VisualTreeAsset element) {
    DebugEx.Info($"Element: {element}, styles");
    foreach (var sheet in element.stylesheets) {
      DebugEx.Info("Element: {0}, style: {1}, importedWithErrors: {2}, importedWithWarnings: {3}",
                   element, sheet.name, sheet.importedWithErrors, sheet.importedWithWarnings);
    }
  }

  static void DumpClasses(VisualElement element) {
    DebugEx.Info($"Element: {element}, classes={DebugEx.C2S(element.classList)}");
    foreach (var child in element.Children()) {
      DumpClasses(child);
    }
  }
}

static class VisualElementExtensions {
  public static T Q2<T>(this VisualElement element, string name) where T : VisualElement {
    var result = element.Q<T>(name);
    if (result == null) {
      throw new InvalidOperationException($"Element {element} does not have child {name}");
    }
    return result;
  }
}
