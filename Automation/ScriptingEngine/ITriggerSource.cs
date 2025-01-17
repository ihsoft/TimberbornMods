// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a trigger source that can be used in the scripting engine.</summary>
/// <remarks>
/// Trigger has either string or numeric value. When the value changes, a callback is called and the trigger instance
/// can be used to get the current value.
/// </remarks>
/// <see cref="ScriptingService.GetTriggerSource"/>
interface ITriggerSource {
  //FIXME: use ScriptValue.ValueType instead
  public enum ValueType {
    Number,
    String,
  }

  /// <summary>The type of the signal value.</summary>
  public ValueType Type { get; }

  /// <summary>Current numeric value of the trigger.</summary>
  /// <remarks>All numbers are 2-digits fixed point numbers. Value "1234" should be treated as "12.34f".</remarks>
  /// <exception cref="ScriptError">if the trigger doesn't provide numeric values.</exception>
  public int NumberValue { get; }

  /// <summary>Current string value of the trigger.</summary>
  /// <exception cref="ScriptError">if the trigger doesn't provide string values.</exception>
  public string StringValue { get; }

  /// <summary>Disposes the trigger source and cancels all callbacks for it.</summary>
  /// <remarks>This method must be called to let the system know the value shouldn't be tracked anymore.</remarks>
  public abstract void Dispose();
}
