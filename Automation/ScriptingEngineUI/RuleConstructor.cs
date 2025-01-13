// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class RuleConstructor : BaseConstructor {
  public override VisualElement Root { get; }

  public readonly ConditionConstructor ConditionConstructor;
  public readonly ActionConstructor ThenActionConstructor;

  public RuleConstructor(UiFactory uiFactory) : base(uiFactory) {
    ConditionConstructor = new ConditionConstructor(uiFactory);
    ThenActionConstructor = new ActionConstructor(uiFactory, false);
    Root = new VisualElement();
    Root.Add(ConditionConstructor.Root);
    Root.Add(ThenActionConstructor.Root);
  }
}
