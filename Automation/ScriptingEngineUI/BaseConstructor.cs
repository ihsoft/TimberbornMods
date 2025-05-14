// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

abstract class BaseConstructor(UiFactory uiFactory) {

  public abstract VisualElement Root { get; }

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
          var element = uiFactory.CreateLabel(classes: [UiFactory.GameTextBigClass]);
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

  protected static string PrepareConstantValue(string value, ScriptValue.TypeEnum type) {
    return type switch {
        ScriptValue.TypeEnum.Number => Mathf.RoundToInt(float.Parse(value) * 100).ToString(),
        ScriptValue.TypeEnum.String => "'" + value + "'",
        _ => throw new InvalidOperationException("Unknown argument type: " + type),
    };
  }
}
