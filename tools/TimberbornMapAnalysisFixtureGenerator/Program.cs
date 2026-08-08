// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.MapAnalysisFixtureGeneration;

static class Program {
  public static int Main(string[] args) {
    var generator = new MapAnalysisFixtureGenerator();
    if (args is ["--water", var map, var fixturePath, var workshopId]) {
      return generator.WriteWaterFixture(map, fixturePath, workshopId);
    }
    if (args is ["--forest", var forestMap, var forestFixturePath, var forestWorkshopId]) {
      return generator.WriteForestFixture(forestMap, forestFixturePath, forestWorkshopId);
    }
    Console.Error.WriteLine(
        "Usage: TimberbornMapAnalysisFixtureGenerator --water MAP.timber OUTPUT.json.gz WORKSHOP_ID\n"
        + "   or: TimberbornMapAnalysisFixtureGenerator --forest MAP.timber OUTPUT.json.gz WORKSHOP_ID");
    return 2;
  }
}
