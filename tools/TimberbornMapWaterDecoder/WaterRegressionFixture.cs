using System.IO.Compression;
using System.Text.Json;

static class WaterRegressionFixture {
  public static void Write(string path, string workshopId, DecodedWaterMap map) {
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using var file = File.Create(path);
    using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
    JsonSerializer.Serialize(gzip, new FixtureData(
        workshopId, map.Width, map.Height, map.SurfaceFloors, map.SurfaceDepths,
        map.SurfaceFlowCoherences, map.SurfaceFlowEdges));
  }

  public static DecodedWaterMap Read(string path) {
    using var file = File.OpenRead(path);
    using var gzip = new GZipStream(file, CompressionMode.Decompress);
    var fixture = JsonSerializer.Deserialize<FixtureData>(gzip)
        ?? throw new InvalidDataException($"Water fixture is empty: {path}");
    var area = checked(fixture.Width * fixture.Height);
    return new DecodedWaterMap(
        fixture.Width, fixture.Height, new int[area], fixture.SurfaceFloors, fixture.SurfaceDepths,
        new float[area], fixture.SurfaceFlowCoherences, fixture.SurfaceFlowEdges, 0, 0);
  }

  sealed record FixtureData(
      string WorkshopId, int Width, int Height, int[] SurfaceFloors, float[] SurfaceDepths,
      float[] SurfaceFlowCoherences, IReadOnlyList<SurfaceFlowEdge> SurfaceFlowEdges);
}
