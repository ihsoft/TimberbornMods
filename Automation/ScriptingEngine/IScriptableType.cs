// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for scriptable types that are available for scripting.</summary>
public interface IScriptableType {
  /// <summary>The name under which the engine will parse the type.</summary>
  string ScriptableTypeName { get; }
}
