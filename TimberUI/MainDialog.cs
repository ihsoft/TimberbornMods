// Timberborn Mod: TimberUI
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.TimberUI;

sealed class MainDialog : IPanelController, ILoadableSingleton {

  readonly PanelStack _panelStack;
  readonly VisualElementLoader _visualElementLoader;
  readonly IAssetLoader _assetLoader;
  readonly UiFactory _uiFactory;

  VisualElement _root;

  MainDialog(PanelStack panelStack, VisualElementLoader visualElementLoader, IAssetLoader assetLoader, UiFactory uiFactory) {
    _panelStack = panelStack;
    _visualElementLoader = visualElementLoader;
    _assetLoader = assetLoader;
    _uiFactory = uiFactory;
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
    _root = GetDialogBox();
    _root.style.position = Position.Absolute;
    var content = _root.Q<ScrollView>("Content");
    var panel = _uiFactory.LoadTimberDevElement("IgorZ/TimberUI-Dialog");
    content.Add(panel);

    //FIXME
    // var style = _assetLoader.Load<StyleSheet>("UI/Views/Common/CommonStyle");
    // var str = StyleSheetToUss.ToUssString(style);
    // DebugEx.Warning("*** CommonStyle:\n{0}", str);

    _panelStack._root.Add(_root);
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
