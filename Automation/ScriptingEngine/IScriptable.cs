// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using JetBrains.Annotations;
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

    public Type ArgumentType { get; init; }
    public string Units { get; init; }
    public DropdownItem<string>[] Options { get; init; }
  }

  public struct TriggerDef {
    public string FullName { get; init; }
    public string DisplayName { get; init; }
    public ArgumentDef ValueType { get; init; }
  }

  public class ActionDef {
    public string FullName { get; init; }
    public string DisplayName { get; init; }
    public ArgumentDef[] ArgumentTypes { get; init; }
  }

  /// <summary>The name of the scriptable component.</summary>
  public string Name { get; }

  /// <summary>The type of the component that this scriptable works on. Global scriptables can have it NULL.</summary>
  public Type InstanceType { get; }

  //FIXME: bad consept since defs can be dynamic (based on the building, e.g. recipies or inventory)
  //[NotNull] LOH
  //public TriggerDef[] Triggers { get; }

  /// <summary>Returns a trigger source that can be used to monitor the specified trigger.</summary>
  /// <param name="name">The name of the trigger.</param>
  /// <param name="building">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <param name="onValueChanged">The callback that si called when the trigger value changes.</param>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged);

  /// <summary>Returns the definition of the trigger with the specified name.</summary>
  /// <param name="name">The name of the trigger.</param>
  /// <param name="building">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <exception cref="ScriptError">if trigger is not found.</exception>
  public TriggerDef GetTriggerDefinition(string name, BaseComponent building);

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="instance">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <param name="args">The arguments for the action. The number, type, and meaning depend on the action.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action GetActionExecutor(string name, BaseComponent instance, string[] args);

  /// <summary>Returns the definition of the action with the specified name.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="instance">
  /// The component on which the action is to be executed. It must be of type <see cref="InstanceType"/>.
  /// </param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name, BaseComponent instance);
}
