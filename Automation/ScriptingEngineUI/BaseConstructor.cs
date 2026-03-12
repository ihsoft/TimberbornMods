// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

abstract class BaseConstructor(UiFactory uiFactory) {

  public abstract VisualElement Root { get; }

  protected readonly UiFactory UIFactory = uiFactory;

  protected VisualElement MakeRow(params object[] arguments) {
    var rowPanel = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
            alignItems = Align.Center,
        },
    };
    foreach (var arg in arguments) {
      switch (arg) {
        case string text:
          var element = UIFactory.CreateLabel(classes: [UiFactory.GameTextBigClass]);
          element.text = text;
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
