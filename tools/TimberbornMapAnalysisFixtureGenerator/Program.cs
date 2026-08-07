// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.MapAnalysisFixtureGeneration;

static class Program {
  public static int Main(string[] args) => new MapAnalysisFixtureGenerator().Run(args);
}

sealed class MapAnalysisFixtureGenerator {
  public int Run(string[] args) {
    if (args is ["--water", var waterMap, var waterFixturePath, var waterWorkshopId]) {
      using var archive = ZipFile.OpenRead(Path.GetFullPath(waterMap));
      using var metadata = ReadJson(archive, "map_metadata.json");
      using var world = ReadJson(archive, "world.json");
      var (width, height) = ReadDimensions(world.RootElement, metadata.RootElement);
      var water = new WaterMapDecoder().Decode(world.RootElement, width, height);
      WaterRegressionFixture.Write(waterFixturePath, waterWorkshopId, water);
      Console.WriteLine($"Wrote {Path.GetFullPath(waterFixturePath)}");
      return 0;
    }

    if (args is ["--forest", var forestMap, var forestFixturePath, var forestWorkshopId]) {
      using var archive = ZipFile.OpenRead(Path.GetFullPath(forestMap));
      using var metadata = ReadJson(archive, "map_metadata.json");
      using var world = ReadJson(archive, "world.json");
      var (width, height) = ReadDimensions(world.RootElement, metadata.RootElement);
      var water = new WaterMapDecoder().Decode(world.RootElement, width, height);
      var landArea = checked(width * height) - water.OpenWaterTileCount;
      ForestRegressionFixture.Write(forestFixturePath, forestWorkshopId, world.RootElement, landArea);
      Console.WriteLine($"Wrote {Path.GetFullPath(forestFixturePath)}");
      return 0;
    }

    Console.Error.WriteLine(
        "Usage: TimberbornMapAnalysisFixtureGenerator --water MAP.timber OUTPUT.json.gz WORKSHOP_ID\n"
        + "   or: TimberbornMapAnalysisFixtureGenerator --forest MAP.timber OUTPUT.json.gz WORKSHOP_ID");
    return 2;
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
