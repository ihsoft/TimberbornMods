using System.Linq;
using Timberborn.CoreUI;
using Timberborn.MainMenuPanels;
using Timberborn.SingletonSystem;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser;

sealed class MainMenuMapBrowserButton : IUpdatableSingleton {
  readonly MainMenuPanel _mainMenuPanel;
  readonly MapBrowserDialog _mapBrowserDialog;

  bool _buttonAdded;

  MainMenuMapBrowserButton(MainMenuPanel mainMenuPanel, MapBrowserDialog mapBrowserDialog) {
    _mainMenuPanel = mainMenuPanel;
    _mapBrowserDialog = mapBrowserDialog;
  }

  public void UpdateSingleton() {
    if (_buttonAdded) {
      return;
    }

    var root = _mainMenuPanel.GetPanel();
    var anchor = root?.Q<Button>("ModManagerButton");
    if (anchor?.parent == null) {
      return;
    }

    var button = new NineSliceButton {
        name = "MapBrowserButton",
        text = "Map Browser",
    };
    button.clicked += _mapBrowserDialog.Show;
    foreach (var className in anchor.GetClasses().ToList()) {
      button.AddToClassList(className);
    }
    anchor.parent.Insert(anchor.parent.IndexOf(anchor) + 1, button);
    _buttonAdded = true;
  }
}
