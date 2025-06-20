// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Exception that is thrown when a script error occurs.</summary>
/// <remarks>Such errors don't fail the game, but they cancel any script execution on the building.</remarks>
abstract class ScriptError : Exception {

  const string ValueOutOfRangeLocKey = "IgorZ.Automation.Scripting.Error.ValueOutOfRange";
  const string DivisionByZeroLocKey = "IgorZ.Automation.Scripting.Error.DivisionByZero";
  const string BadValueLocKey = "IgorZ.Automation.Scripting.Error.BadValue";
  const string InternalErrorLocKey = "IgorZ.Automation.Scripting.Error.InternalError";

  /// <summary>Creates a new instance of the exception.</summary>
  ScriptError(string message) : base(message) {}

  /// <summary>Error during the script execution.</summary>
  /// <remarks>This indicated an unrecoverable error on the script.</remarks>
  public abstract class RuntimeError(string locKey, string reason) : ScriptError(reason) {
    public string LocKey { get; } = locKey;
  }

  /// <summary>Error that indicates that the value is "in general" bad.</summary>
  /// <remarks>Provide an optional reason string to explain what was the value and what was expected.</remarks>
  /// <seealso cref="ScriptError.ValueOutOfRange"/>
  public class BadValue : RuntimeError {
    /// <summary>Error that indicates that the value is "in general" bad.</summary>
    /// <remarks>Provide an optional reason string to explain what was the value and what was expected.</remarks>
    /// <seealso cref="ScriptError.ValueOutOfRange"/>
    public BadValue(string reason = null) : base(BadValueLocKey, reason ?? "Bad value") {}

    /// <summary>The constructor to create the specific "bad value" exceptions.</summary>
    protected BadValue(string locKey, string reason) : base(locKey, reason) {}
  }

  /// <summary>Error that indicates that the value is not in the expected range.</summary>
  /// <remarks>Provide an optional reason string to explain what was the value and what was expected.</remarks>
  public class ValueOutOfRange(string reason = null) : BadValue(ValueOutOfRangeLocKey, reason ?? "Value out of range");

  /// <summary>Error that indicates that the script tried to divide by zero.</summary>
  public class DivisionByZero() : RuntimeError(DivisionByZeroLocKey, "Division by zero");

  /// <summary>Error that indicates an internal error in the script engine.</summary>
  /// <remarks>
  /// This kind of error marks the cases when the script has major problems in general. Such errors are unrecoverable.
  /// </remarks>
  public class InternalError(string reason = null) : RuntimeError(InternalErrorLocKey, reason ?? "Internal error");

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
