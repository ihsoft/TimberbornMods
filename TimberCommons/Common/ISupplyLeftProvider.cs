// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.TimberCommons.Common;

/// <summary>Interface that enables "supply lasts for XX" progress bar in UI for manufactories.</summary>
/// <remarks>
/// It's expected that only one component on the building implements it. If there is such a component, and it returns a
/// non "null" value, then the UI fragment gets a "progress bar" that tells player how long the buildings would work on
/// the existing inventory reserves.
/// </remarks>
public interface ISupplyLeftProvider {
  /// <summary>Returns the stats or indicates that the progress bar should be hidden.</summary>
  /// <remarks>
  /// This method will be called from the UI fragment code, which happens every frame. Avoid doing expensive stuff in
  /// this method.
  /// </remarks>
  /// <returns>
  /// The progress as a value in range [0; 1] and the message to show on the progress bar. The progress must be greater
  /// than zero and the message must not be <c>null</c>. If any of these conditions is not met, the progress bar will be
  /// hidden in the UI.
  /// </returns>
  public (float progress, string progressBarMsg) GetStats();
}