// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopLiveDetailsResult {
  public WorkshopLiveDetailsResult(WorkshopLiveDetails details, bool unavailable, string error) {
    Details = details;
    Unavailable = unavailable;
    Error = error;
  }

  /// <summary>Live statistics and gallery returned for an available Workshop item.</summary>
  public WorkshopLiveDetails Details { get; }

  /// <summary>Whether Steam confirmed that the requested Workshop item is no longer available.</summary>
  public bool Unavailable { get; }

  /// <summary>Diagnostic Steam result when live details could not be obtained.</summary>
  public string Error { get; }
}
