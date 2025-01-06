// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using TimberApi.UIPresets.Labels;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

abstract class BaseConstructor(UiFactory uiFactory) {

  public abstract VisualElement Root { get; }

  protected VisualElement MakeRow(params object[] arguments) {
    var rowPanel = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
        }
    };
    foreach (var arg in arguments) {
      switch (arg) {
        case string text:
          var element = uiFactory.UiBuilder.Create<GameTextLabel>().SetText(text).BuildAndInitialize();
          element.style.marginRight = 5;
          rowPanel.Add(element);
          break;
        case VisualElement visualElement:
          rowPanel.Add(visualElement);
          break;
        default:
          throw new InvalidOperationException("Unknown argument type: " + arg.GetType());
      }
    }
    return rowPanel;
  }
}
