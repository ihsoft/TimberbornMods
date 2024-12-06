// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.ScriptingEngine.Nodes;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.Values;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Automation script component that automates the building.</summary>
sealed class AutomationScript : BaseComponent {

  public string LastError { get; private set; }

  public TriggerRule[] TriggerRules { get; private set; } = [];

  string ScriptableObjectName = null;

  Dictionary<string, ITrigger> SelfTriggers => _triggers ??= GetTriggers(this);
  Dictionary<string, ITrigger> _triggers;

  Dictionary<string, IScriptableInstance> SelfInstances => _instances ??= GetInstances(this);
  Dictionary<string, IScriptableInstance> _instances;

  ScriptingService _scriptingService;

  [Inject]
  public void InjectDependencies(ScriptingService scriptingService) {
    _scriptingService = scriptingService;
  }

  public void Compile(string code) {
    try {
      CompileInternal(code);
      LastError = null;
    } catch (ScriptError e) {
      LastError = e.Message;
      HostedDebugLog.Error(this, "Error compiling script: {0}", LastError);
    }
  }

  public void Erase() {
    foreach (var trigger in TriggerRules) {
      trigger.OnTriggerDestroyed();
    }
    TriggerRules = [];
  }

  #region Implementation

  void CompileInternal(string code) {
    // TEST CODE BELOW
    // On Self.Floodgate.HeightChangedEvent():
    //   $Debug.Log("Height changed to {0}", Self.Floodgate.GetHeight());

    //Line #1: On Self.Floodgate.HeightChangedEvent():
    var triggerTypeName = "Floodgate";
    AutomationScript self = this;  // Self
    if (!self.SelfTriggers.TryGetValue(triggerTypeName, out var trigger)) {
      throw new ScriptError($"Type {triggerTypeName} not found on: {DebugEx.ObjectToString(self)}");
    }
    var eventName = "HeightChangedEvent";
    IExpressionValue[] args = [];  // Constant values.
    var triggerNode = new TriggerRule(trigger, eventName, args);

    //FIXME: use "ad as we go" approach for the args.
    //Line #2: $Debug.Log1("Height changed to {0}", Self.Floodgate.GetHeight());
    ExpressionNode arg1 = new ConstantValueNode(StringValue.FromLiteral("Height changed to {0}"));
    var instanceTypeName = "Floodgate";
    if (!self.SelfInstances.TryGetValue(instanceTypeName, out var floodgateInstance)) {
      throw new ScriptError($"Type {instanceTypeName} not found on: {DebugEx.ObjectToString(self)}");
    }
    ExpressionNode arg2 = new FunctionNode(floodgateInstance, "GetHeight", []);
    var globalName = "Debug";
    IScriptableInstance debug = _scriptingService.GetGlobalInstance(globalName);
    ExpressionNode statement = new FunctionNode(debug, "Log1", [arg1, arg2]);
    triggerNode.AddStatement(statement);

    TriggerRules = [triggerNode];
  }

  static Dictionary<string, ITrigger> GetTriggers(BaseComponent instance) {
    var triggerComponents = new List<ITrigger>();
    instance.GetComponentsFast(triggerComponents);
    return triggerComponents.ToDictionary(x => x.ScriptableTypeName, x => x);
  }

  static Dictionary<string, IScriptableInstance> GetInstances(BaseComponent instance) {
    var triggerComponents = new List<IScriptableInstance>();
    instance.GetComponentsFast(triggerComponents);
    return triggerComponents.ToDictionary(x => x.ScriptableTypeName, x => x);
  }

  #endregion
}
