// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;

namespace IgorZ.MapBrowser.WaterDecoder;

static class ForestRegressionFixture {
  sealed record FixtureData(string WorkshopId, int LandArea, IReadOnlyList<JsonElement> Entities);

  public static void Write(string path, string workshopId, JsonElement world, int landArea) {
    var entities = world.GetProperty("Entities").EnumerateArray()
        .Where(IsRelevantNaturalResource)
        .Select(entity => entity.Clone())
        .ToArray();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var file = File.Create(path);
    using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
    JsonSerializer.Serialize(gzip, new FixtureData(workshopId, landArea, entities));
  }

  public static (JsonElement World, int LandArea) Read(string path) {
    using var file = File.OpenRead(path);
    using var gzip = new GZipStream(file, CompressionMode.Decompress);
    var fixture = JsonSerializer.Deserialize<FixtureData>(gzip)
        ?? throw new InvalidDataException($"Forest fixture is empty: {path}");
    return (JsonSerializer.SerializeToElement(new { fixture.Entities }), fixture.LandArea);
  }

  static bool IsRelevantNaturalResource(JsonElement entity) {
    if (!entity.TryGetProperty("Components", out var components)
        || components.ValueKind != JsonValueKind.Object) {
      return false;
    }
    return components.TryGetProperty("LivingNaturalResource", out _)
        || components.TryGetProperty("Yielder:Cuttable", out _);
  }
}
