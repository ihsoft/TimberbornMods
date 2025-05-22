// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Exception that is thrown when a script error occurs.</summary>
/// <remarks>Such errors don't fail the game, but they cancel any script execution on the building.</remarks>
class ScriptError : Exception {
  /// <summary>Creates a new instance of the exception.</summary>
  protected ScriptError(string message) : base(message) {}

  /// <summary>Error during the script execution.</summary>
  /// <remarks>This indicated an unrecoverable error on the script.</remarks>
  public class RuntimeError(string reason) : ScriptError(reason);

  /// <summary>The script source is invalid and can't be properly parsed.</summary>
  public class ParsingError(string reason) : ScriptError(reason);

  /// <summary>The component state is not suitable for the expression.</summary>
  /// <remarks>
  /// This error is only produced during the parsing stage. If the component state becomes bad after the successful
  /// parsing, then it should be reported as <see cref="ScriptError.RuntimeError"/>.
  /// </remarks>
  public class BadStateError(BaseComponent component, string reason) : ScriptError(reason) {
    public override string ToString() => $"{DebugEx.ObjectToString(component)}: {Message}";
  }
}
