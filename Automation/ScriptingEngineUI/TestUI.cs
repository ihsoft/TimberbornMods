// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class TestUI : IPanelController, ILoadableSingleton {

  readonly PanelStack _panelStack;
  readonly VisualElementLoader _visualElementLoader;
  readonly RulesEditorDialog _rulesEditorDialog;

  VisualElement _root;

  TestUI(PanelStack panelStack, VisualElementLoader visualElementLoader, RulesEditorDialog rulesEditorDialog) {
    DebugEx.Warning("*** TestUI");
    _panelStack = panelStack;
    _visualElementLoader = visualElementLoader;
    _rulesEditorDialog = rulesEditorDialog;
  }

  public VisualElement GetPanel() {
    return _root;
  }

  public bool OnUIConfirmed() {
    DebugEx.Warning("*** OnUIConfirmed");
    return false;
  }

  public void OnUICancelled() {
    DebugEx.Warning("*** OnUICancelled");
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
    _panelStack._root.Add(_root);
    _root.Q<ScrollView>("Content").Add(_rulesEditorDialog.Root);
  }
}