// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.MapAnalysisFixtureGeneration;

static class WaterRegressionFixture {
  sealed record FixtureData(
      string WorkshopId, int Width, int Height, int[] TerrainHeights, int[] SurfaceFloors, float[] SurfaceDepths,
      float[] SurfaceFlowCoherences, IReadOnlyList<SurfaceFlowEdge> SurfaceFlowEdges);

  public static void Write(string path, string workshopId, DecodedWaterMap map) {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var file = File.Create(path);
    using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
    JsonSerializer.Serialize(gzip, new FixtureData(
        workshopId, map.Width, map.Height, map.TerrainHeights, map.SurfaceFloors, map.SurfaceDepths,
        map.SurfaceFlowCoherences, map.SurfaceFlowEdges));
  }

  public static DecodedWaterMap Read(string path) {
    using var file = File.OpenRead(path);
    using var gzip = new GZipStream(file, CompressionMode.Decompress);
    var fixture = JsonSerializer.Deserialize<FixtureData>(gzip)
        ?? throw new InvalidDataException($"Water fixture is empty: {path}");
    var area = checked(fixture.Width * fixture.Height);
    var terrainHeights = fixture.TerrainHeights ?? fixture.SurfaceFloors;
    return new DecodedWaterMap(
        fixture.Width, fixture.Height, terrainHeights, fixture.SurfaceFloors, fixture.SurfaceDepths,
        new float[area], fixture.SurfaceFlowCoherences, fixture.SurfaceFlowEdges, 0, 0);
  }
}
