// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

static class MapPayloadCachePolicy {
  public static bool ShouldPopulate(string? collectionState, bool needsRefresh, bool payloadCached) {
    return collectionState != "unsupported" && !needsRefresh && !payloadCached;
  }
}
