// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for and instance that exposes scriptable methods.</summary>
interface IScriptableInstance : IScriptableType {
  /// <summary>Attribute to apply to the methods that should be exposed.</summary>
  /// <remarks>The arguments and return type must be of type <see cref="IExpressionValue"/>.</remarks>
  public sealed class ScriptFunctionAttribute : Attribute;
}
