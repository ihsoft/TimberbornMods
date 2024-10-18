// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core {

/// <summary>Interface for components that can adjust the power input of the building.</summary>
/// <remarks>
/// Just implement the interface in any component and it will be called when <see cref="MechanicalBuilding"/> needs an
/// update.
/// </remarks>
public interface IAdjustablePowerInput {
  /// <summary>Updates the internal component state and returns the latest value of the power input.</summary>
  /// <remarks>
  /// The callback is responsible to check all the conditions. The returned value will be used "as-is" to update the
  /// node input power. Thus, even the paused buildings will consume power if the callback returns a non-zero value.
  /// </remarks>
  /// <param name="nominalPowerInput">The power that's the building normally consumes.</param>
  /// <returns>The actual power than the building should consume.</returns>
  int UpdateAndGetPowerInput(int nominalPowerInput);
}

}
