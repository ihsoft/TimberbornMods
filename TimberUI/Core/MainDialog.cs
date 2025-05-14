// Timberborn Mod: TimberUI
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEditor.StyleSheets;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace IgorZ.TimberUI.Core;

sealed class MainDialog : ILoadableSingleton {

  readonly PanelStack _panelStack;
  readonly IAssetLoader _assetLoader;
  readonly UiFactory _uiFactory;
  readonly TimberUISettings _settings;

  Label _logTextLabel;
  Label _errorText;
  TextField _resourcePathTextField;
  TextField _filenameTextField;

  MainDialog(PanelStack panelStack, IAssetLoader assetLoader, UiFactory uiFactory, TimberUISettings settings) {
    _panelStack = panelStack;
    _assetLoader = assetLoader;
    _uiFactory = uiFactory;
    _settings = settings;
  }

  public void Load() {
    var dialog = _uiFactory.LoadVisualTreeAsset("IgorZ.TimberUI/MainDialog");
    dialog.Q2<Label>("Header").text = "Timberborn UI Exporter";
    dialog.Q2<Button>("CloseButton").clicked += () => _panelStack._root.Remove(dialog);
    _resourcePathTextField = dialog.Q2<TextField>("ResourcePath");
    _resourcePathTextField.value = "UI/Views/";
    _filenameTextField = dialog.Q2<TextField>("Filename");
    _filenameTextField.value = _settings.TargetPath.Value;
    dialog.Q2<Button>("ExportUSSButton").clicked += ExportUss;
    dialog.Q2<Button>("ExportUXMLButton").clicked += ExportUxml;
    dialog.Q2<Button>("PrintStylesButton").clicked += PrintStyles;
    _logTextLabel = dialog.Q2<Label>("LogText");
    _logTextLabel.ToggleDisplayStyle(false);
    _errorText = dialog.Q2<Label>("ErrorText");
    _errorText.ToggleDisplayStyle(false);

    dialog.style.position = Position.Absolute;
    dialog.style.top = 20;
    dialog.style.left = 20;
    _panelStack._root.Add(dialog);
  }

  void ExportUss() {
    _errorText.ToggleDisplayStyle(false);
    _logTextLabel.ToggleDisplayStyle(false);
    var fullPath = _filenameTextField.value;
    if (!fullPath.EndsWith(".uss")) {
      fullPath = Path.Combine(fullPath, _resourcePathTextField.value + ".uss");
    }
    var error = TryLoadAsset<StyleSheet>(_resourcePathTextField.value, out var style)
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

  void ExportUxml() {
    _errorText.ToggleDisplayStyle(false);
    _logTextLabel.ToggleDisplayStyle(false);
    var fullPath = _filenameTextField.value;
    if (!fullPath.EndsWith(".uxml")) {
      fullPath = Path.Combine(fullPath, _resourcePathTextField.value + ".uxml");
    }
    var sb = new StringBuilder();
    var error = TryLoadAsset<VisualTreeAsset>(_resourcePathTextField.value, out var treeAsset)
        ?? VerifyTargetPath(fullPath)
        ?? VisitNodes(sb, treeAsset);
    if (error != null) {
      _errorText.text = error;
      _errorText.ToggleDisplayStyle(true);
      return;
    }
    using (var writer = new StreamWriter(fullPath)) {
      writer.Write(sb.ToString());
    }
    _logTextLabel.text = $"Successfully exported {treeAsset.name} to {fullPath}";
    _logTextLabel.ToggleDisplayStyle(true);
  }

  static string VisitNodes(StringBuilder sb, VisualTreeAsset tree) {
    var dictionary = new Dictionary<int, List<VisualElementAsset>>();
    var visualAssetsCount = tree.visualElementAssets?.Count ?? 0;
    var templateAssetsCount = tree.templateAssets?.Count ?? 0;
    for (var i = 0; i < visualAssetsCount + templateAssetsCount; i++) {
      var visualElementAsset =
          i < visualAssetsCount ? tree.visualElementAssets![i] : tree.templateAssets![i - visualAssetsCount];
      if (!dictionary.TryGetValue(visualElementAsset.parentId, out var visualElementAssets)) {
        visualElementAssets = [];
        dictionary[visualElementAsset.parentId] = visualElementAssets;
      }
      visualElementAssets.Add(visualElementAsset);
    }
    dictionary.TryGetValue(0, out var documentRoot);
    if (documentRoot == null || documentRoot.Count == 0) {
      return "Cannot parse tree asset";
    }
    if (documentRoot.Count != 1 || documentRoot[0].fullTypeName != "UnityEngine.UIElements.UXML") {
      return "Cannot parse tree asset: not a UXML";
    }
    var rootElement = documentRoot[0];
    dictionary.TryGetValue(rootElement.id, out var elementAssets);
    if (elementAssets == null || elementAssets.Count == 0) {
      return null;
    }
    var target = new VisualElementRecord { Asset = rootElement, TreeAsset = tree };
    elementAssets.Sort(CompareForOrder);
    foreach (var item in elementAssets) {
      target.Hierarchy.Add(VisitNodesRecursively(item, tree, dictionary));
    }
    target.PrintTree(sb, 0);
    return null;
  }

  static VisualElementRecord VisitNodesRecursively(
      VisualElementAsset root, VisualTreeAsset tree, Dictionary<int, List<VisualElementAsset>> idToChildren) {
    var visualElement = new VisualElementRecord { Asset = root, TreeAsset = tree };
    if (!idToChildren.TryGetValue(root.id, out var children)) {
      return visualElement;
    }
    children.Sort(CompareForOrder);
    foreach (var child in children) {
      visualElement.Hierarchy.Add(VisitNodesRecursively(child, tree, idToChildren));
    }
    return visualElement;
  }

  static int CompareForOrder(VisualElementAsset a, VisualElementAsset b) {
    return a.orderInDocument.CompareTo(b.orderInDocument);
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
    var sb = new StringBuilder();
    var styles = asset.stylesheets.Select(x => ResolveStyle(x.name)).ToList();
    sb.AppendLine("Styles:\n" + string.Join("\n", styles));
    sb.AppendLine();
    var templates = asset.templateAssets.Select(x => ResolveTemplate(x.templateAlias)).ToList();
    sb.AppendLine("Templates:\n" + string.Join("\n", templates));
    _logTextLabel.text = sb.ToString();
    _logTextLabel.ToggleDisplayStyle(true);
  }

  static string ResolveStyle(string name) {
    if (VisualElementRecord.StandardStylesPaths.TryGetValue(name, out var path)) {
      return path;
    }
    return name + ".uss";
  }

  static string ResolveTemplate(string name) {
    if (VisualElementRecord.StandardTemplatesPaths.TryGetValue(name, out var path)) {
      return path;
    }
    return name + ".uxml";
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

  string TryLoadAsset<T>(string path, out T asset) where T : Object {
    try {
      asset = _assetLoader.Load<T>(path);
      return null;
    } catch (Exception e) {
      asset = null;
      return e.Message;
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
