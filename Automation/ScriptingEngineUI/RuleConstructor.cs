// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleConstructor : BaseConstructor {
  public override VisualElement Root { get; }

  public readonly ConditionConstructor ConditionConstructor;
  public readonly ActionConstructor ActionConstructor;

  public RuleConstructor(UiFactory uiFactory) : base(uiFactory) {
    ConditionConstructor = new ConditionConstructor(uiFactory);
    ConditionConstructor.Root.style.marginBottom = 5;
    ActionConstructor = new ActionConstructor(uiFactory);
    Root = new VisualElement();
    Root.Add(ConditionConstructor.Root);
    Root.Add(ActionConstructor.Root);
  }
}
