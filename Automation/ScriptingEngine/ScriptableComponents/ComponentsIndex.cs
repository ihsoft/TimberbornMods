// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

static class ComponentsIndex {
  /// <summary>Defines all types that are available for scripting. Not all instances can have all of them.</summary>
  public static Dictionary<string, Type> NameToType = new() {
      { "Debug", typeof(DebugScriptableComponent) },
      { "Floodgate", typeof(FloodgateScriptableComponent) },
  };

  /// <summary>Gives a reverse lookup to get the type name.</summary>
  public static Dictionary<Type, string> TypeToName = NameToType.ToDictionary(kv => kv.Value, kv => kv.Key);
}
