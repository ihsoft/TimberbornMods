// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Text;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class AutomationScriptFragment : IEntityPanelFragment {
  readonly UiFactory _uiFactory;

  VisualElement _root;
  Label _caption;
  Label _stats;
  Button _addTestScriptButton;

  AutomationScript _automationScript;

  AutomationScriptFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  public VisualElement InitializeFragment() {
    _caption = _uiFactory.CreateLabel();
    _caption.style.color = Color.cyan;

    _stats = _uiFactory.CreateLabel();
    //_stats.ToggleDisplayStyle(false);

    _addTestScriptButton = _uiFactory.CreateButton("AddTestScript", AddTestScript);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_caption)
        .AddComponent(_stats)
        .AddComponent(_addTestScriptButton)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _automationScript = entity.GetComponentFast<AutomationScript>();
    _root.ToggleDisplayStyle(visible: _automationScript);
  }

  public void ClearFragment() {
    _root.ToggleDisplayStyle(false);
    _automationScript = null;
  }

  public void UpdateFragment() {
    if (!_automationScript) {
      return;
    }

    string status;
    if (_automationScript.LastError != null) {
      status = HasErrors;
    } else if (_automationScript.TriggerRules.Length > 0) {
      status = CompiledSuccessfully;
    } else {
      status = NoAutomation;
    }
    _caption.text = TextColors.ColorizeText(string.Format(AutomationScriptStatus, status));

    if (_automationScript.TriggerRules.Length == 0 || _automationScript.LastError != null) {
      _stats.text = "";
      return;
    }

    var stats = new StringBuilder();
    stats.AppendLine("Triggers: ");
    foreach (var trigger in _automationScript.TriggerRules) {
      stats.Append(SpecialStrings.RowStarter);
      stats.Append(trigger.Trigger.ScriptableTypeName + "." + trigger.Name + ":");
      foreach (var target in trigger.AffectedTargets) {
        stats.Append(trigger.AffectedTargets.Length > 1 ? SpecialStrings.RowStarter : " ");
        stats.AppendLine(target);
      }
    }
    _stats.text = stats.ToString();
  }

  const string HasErrors = "<RedHighlight>ERRORS</RedHighlight>";
  const string CompiledSuccessfully = "<GreenHighlight>RUNNING</GreenHighlight>";
  const string NoAutomation = "No script";
  const string AutomationScriptStatus = "Automation Script status: {0}";

  void AddTestScript() {
    //FIXME
    DebugEx.Warning("*** Before addding test script ***");
    _automationScript.Compile("test");
    DebugEx.Warning("*** After addding test script ***");
  }
}