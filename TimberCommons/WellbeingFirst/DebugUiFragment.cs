// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using System.Text;
using IgorZ.TimberDev.UI;
using TimberApi.UiBuilderSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WellbeingFirst {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class DebugUiFragment : IEntityPanelFragment {
  readonly UIBuilder _builder;
  
  VisualElement _root;
  Label _infoLabel;
  Button _resetBehavior;
  HaulerWellbeingOptimizer _optimizer;
  
  public DebugUiFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _infoLabel = _builder.Presets().Labels().Label(color: UiFactory.PanelNormalColor);
    _resetBehavior = _builder.Presets().Buttons().Button(text: "Cancel current work");
    _resetBehavior.clicked += () => {
      _optimizer.CancelCurrentBehavior();
    };

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_infoLabel)
        .AddComponent(_resetBehavior)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _optimizer = entity.GetComponentFast<HaulerWellbeingOptimizer>();
    _root.ToggleDisplayStyle(visible: _optimizer);
    // if (_citizen) {
    //   PrintAllComponents(_citizen);
    // }
  }

  static void PrintAllComponents(BaseComponent component) {
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Components on {DebugEx.BaseComponentToString(component)}:");
    var names = component.AllComponents.Select(x => x.GetType().ToString()).OrderBy(x => x);
    lines.AppendLine(string.Join("\n", names));
    lines.AppendLine(new string('*', 10));
    
    DebugEx.Warning(lines.ToString());
  }

  public void ClearFragment() {
    _optimizer = null;
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
    if (!_optimizer) {
      return;
    }
    string info = "";
    var needs = _optimizer.NeedsInCriticalState.ToList();
    if (needs.Count > 0) {
      info = "Needs in critical state: " + string.Join(",", needs);
    }
    var behavior = _optimizer.BehaviorManager._runningBehavior;
    if (behavior) {
      info += "\nBehavior: " + behavior.ComponentName + DebugEx.ObjectToString(behavior);
    }
    _infoLabel.text = info;
    _resetBehavior.ToggleDisplayStyle(visible: _optimizer.CheckCanCancelCurrentBehavior());
  }
}

}
