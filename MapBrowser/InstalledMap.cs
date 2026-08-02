using Timberborn.MapItemsUI;

namespace IgorZ.MapBrowser;

sealed record InstalledMap(MapItem Map, string PublishedFileId) {
  public string Key => Map.MapFileReference.Path ?? Map.MapFileReference.Name;
  public bool Removed { get; set; }
}
