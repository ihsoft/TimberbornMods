// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to scripting components and the other global stuff.</summary>
sealed class ScriptingService {

  #region API

  /// <summary>Gets a global instance by its name.</summary>
  public IScriptableInstance GetGlobalInstance(string name) {
    if (!InstancesDict.TryGetValue(name, out var instance)) {
      throw new ScriptError($"Global instance {name} not found");
    }
    return instance;
  }

  /// <summary>Gets a global trigger by its name.</summary>
  public ITrigger GetGlobalTrigger(string name) {
    if (!TriggersDict.TryGetValue(name, out var trigger)) {
      throw new ScriptError($"Global trigger {name} not found");
    }
    return trigger;
  }

  #endregion

  #region Implementation

  static readonly IScriptableInstance[] AllInstances = [
      new DebugScriptableComponent(),
  ];

  static readonly ITrigger[] AllTriggers = [
  ];
  
  static readonly Dictionary<string, IScriptableInstance> InstancesDict =
      AllInstances.ToDictionary(x => x.ScriptableTypeName, x => x);

  static readonly Dictionary<string, ITrigger> TriggersDict =
      AllTriggers.ToDictionary(x => x.ScriptableTypeName, x => x);

  #endregion
}
