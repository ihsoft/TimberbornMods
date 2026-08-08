// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.MapAnalysisFixtureGeneration;

sealed class MapAnalysisFixtureGenerator {
  /// <summary>Extracts the decoded water state needed by water-classifier regression tests.</summary>
  public int WriteWaterFixture(string map, string fixturePath, string workshopId) {
    using var archive = ZipFile.OpenRead(Path.GetFullPath(map));
    using var metadata = ReadJson(archive, "map_metadata.json");
    using var world = ReadJson(archive, "world.json");
    var (width, height) = ReadDimensions(world.RootElement, metadata.RootElement);
    var water = new WaterMapDecoder().Decode(world.RootElement, width, height);
    WaterRegressionFixture.Write(fixturePath, workshopId, water);
    Console.WriteLine($"Wrote {Path.GetFullPath(fixturePath)}");
    return 0;
  }

  /// <summary>Extracts living-resource entities and dry-land area for forest-classifier regression tests.</summary>
  public int WriteForestFixture(string map, string fixturePath, string workshopId) {
    using var archive = ZipFile.OpenRead(Path.GetFullPath(map));
    using var metadata = ReadJson(archive, "map_metadata.json");
    using var world = ReadJson(archive, "world.json");
    var (width, height) = ReadDimensions(world.RootElement, metadata.RootElement);
    var water = new WaterMapDecoder().Decode(world.RootElement, width, height);
    var landArea = checked(width * height) - water.OpenWaterTileCount;
    ForestRegressionFixture.Write(fixturePath, workshopId, world.RootElement, landArea);
    Console.WriteLine($"Wrote {Path.GetFullPath(fixturePath)}");
    return 0;
  }

  static JsonDocument ReadJson(ZipArchive archive, string name) {
    var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Map archive has no {name} entry.");
    using var stream = entry.Open();
    return JsonDocument.Parse(stream);
  }

  static (int Width, int Height) ReadDimensions(JsonElement world, JsonElement metadata) {
    if (world.TryGetProperty("Singletons", out var singletons)
        && singletons.TryGetProperty("MapSize", out var mapSize)
        && mapSize.TryGetProperty("Size", out var size)) {
      return (size.GetProperty("X").GetInt32(), size.GetProperty("Y").GetInt32());
    }
    return (metadata.GetProperty("Width").GetInt32(), metadata.GetProperty("Height").GetInt32());
  }
}
