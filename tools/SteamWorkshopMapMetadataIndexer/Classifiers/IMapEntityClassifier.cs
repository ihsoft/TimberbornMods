// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

interface IMapEntityClassifier {
  string Key { get; }

  void ObserveEntity(JsonElement entity);

  JsonElement BuildResult(MapDimensions mapDimensions, int landArea);
}
