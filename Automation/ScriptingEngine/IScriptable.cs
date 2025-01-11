// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine;

interface IScriptable {
  public struct ArgumentDef {
    public enum Type {
      Number,
      Percentage,
      Float,
      String,
    }

    public Type ArgumentType;
    public string Units;
    public DropdownItem<string>[] Options;
  }

  public class TriggerDef {
    public string Name;
    public string DisplayName;
    public ArgumentDef ValueType;
  }

  public class ActionDef {
    public string Name;
    public Func<string> DisplayName;
    public ArgumentDef[] ArgumentTypes;
    public Action<string[]> Executor;
  }

  public string Name { get; }
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged);
  public TriggerDef GetTriggerDefinition(string name);
}
