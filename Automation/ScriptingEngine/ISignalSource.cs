// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a signal source that can be used in the scripting engine.</summary>
/// <remarks>
/// When the value changes, a callback is called and the signal source instance can be used to get the current value.
/// </remarks>
/// <see cref="ScriptingService.GetSignalSource"/>
interface ISignalSource {
  /// <summary>Current value of the signal.</summary>
  public ScriptValue CurrentValue { get; }

  /// <summary>Disposes the signal source and cancels all callbacks for it.</summary>
  /// <remarks>This method must be called to let the system know the value shouldn't be tracked anymore.</remarks>
  /// FIXME: drop it! refactor how registration for updates is happening.
  public abstract void Dispose();
}
