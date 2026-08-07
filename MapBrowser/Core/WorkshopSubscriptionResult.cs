// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopSubscriptionResult {
  public WorkshopSubscriptionResult(bool succeeded, bool unavailable, string error) {
    Succeeded = succeeded;
    Unavailable = unavailable;
    Error = error;
  }

  /// <summary>Whether Steam accepted the subscription and started downloading the item.</summary>
  public bool Succeeded { get; }

  /// <summary>Whether Steam reported that the Workshop item no longer exists or is inaccessible.</summary>
  public bool Unavailable { get; }

  /// <summary>Diagnostic Steam result for logging when the operation did not succeed.</summary>
  public string Error { get; }
}
