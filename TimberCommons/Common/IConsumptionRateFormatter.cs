// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.TimberCommons.Common {

/// <summary>Interface that helps to customize the tooltip.</summary>
/// <remarks>
/// Any component can provide this interface to show consumption rate in a custom form. If there are multiple components
/// that provide the interface, then only one is used, and it's unspecified which.
/// </remarks>
public interface IConsumptionRateFormatter {
  /// <summary>Returns formatted string of the good consumption rate.</summary>
  string GetRate();

  /// <summary>Returns formatted time for which the rate is counted.</summary>
  /// <remarks>E.g. "10h" or "1d".</remarks>
  string GetTime();
}

}
