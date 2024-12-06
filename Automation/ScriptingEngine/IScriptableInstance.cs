// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for and instance that exposes scriptable methods.</summary>
interface IScriptableInstance : IScriptableType {
  public sealed class ScriptFunctionAttribute : Attribute;
}
