// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.MapItemsUI;

namespace IgorZ.MapBrowser;

sealed record InstalledMap(MapItem Map, string PublishedFileId, WorkshopItemMetadata Metadata = null) {
  public string Key => Map?.MapFileReference.Path ?? Map?.MapFileReference.Name ?? PublishedFileId;
  public bool IsInstalled => Map != null;
  public bool Removed { get; set; }
}
