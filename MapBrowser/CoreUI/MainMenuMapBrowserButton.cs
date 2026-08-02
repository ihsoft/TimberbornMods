// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.MainMenuPanels;
using Timberborn.SingletonSystem;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser.CoreUI;

sealed class MainMenuMapBrowserButton : IUpdatableSingleton {
  const string DialogHeaderLocKey = "IgorZ.MapBrowser.Dialog.Header";

  readonly MainMenuPanel _mainMenuPanel;
  readonly MapBrowserDialog _mapBrowserDialog;
  readonly UiFactory _uiFactory;

  bool _buttonAdded;

  MainMenuMapBrowserButton(MainMenuPanel mainMenuPanel, MapBrowserDialog mapBrowserDialog, UiFactory uiFactory) {
    _mainMenuPanel = mainMenuPanel;
    _mapBrowserDialog = mapBrowserDialog;
    _uiFactory = uiFactory;
  }

  public void UpdateSingleton() {
    if (_buttonAdded) {
      return;
    }

    var root = _mainMenuPanel.GetPanel();
    var anchor = root?.Q<Button>("LoadMapButton");
    if (anchor?.parent == null) {
      return;
    }

    var button = new NineSliceButton {
        name = "MapBrowserButton",
        text = _uiFactory.T(DialogHeaderLocKey),
    };
    button.clicked += _mapBrowserDialog.Show;
    foreach (var className in anchor.GetClasses().ToList()) {
      button.AddToClassList(className);
    }
    anchor.parent.Insert(anchor.parent.IndexOf(anchor) + 1, button);
    _buttonAdded = true;
  }
}
