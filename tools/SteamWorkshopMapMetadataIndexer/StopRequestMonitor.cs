// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

static class StopRequestMonitor {
  public static bool IsStopRequested(string? path) {
    return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
  }
}
