// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

static class SteamReconnectPolicy {
  public static bool ShouldReconnect(int downloadRequestsSinceLogin, int reconnectAfterDownloads) {
    return reconnectAfterDownloads > 0 && downloadRequestsSinceLogin >= reconnectAfterDownloads;
  }
}
