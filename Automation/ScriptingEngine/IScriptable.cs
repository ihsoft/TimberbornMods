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
    public string DisplayName;
    public ArgumentDef[] ArgumentTypes;
  }

  /// <summary>The name of the scriptable component.</summary>
  public string Name { get; }

  /// <summary>The type of the component that this scriptable works on. Global scriptables can have it NULL.</summary>
  public Type InstanceType { get; }

  /// <summary>Returns a trigger source that can be used to monitor the specified trigger.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <param name="onValueChanged">The callback that si called when the trigger value changes.</param>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged);

  /// <summary>Returns the definition of the trigger with the specified name.</summary>
  public TriggerDef GetTriggerDefinition(string name);

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <param name="args">The arguments for the action. The number, type, and meaning depend on the action.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action GetActionExecutor(string name, BaseComponent building, string[] args);

  /// <summary>Returns the definition of the action with the specified name.</summary>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name);
}
