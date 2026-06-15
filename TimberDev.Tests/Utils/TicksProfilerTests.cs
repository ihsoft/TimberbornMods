using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class TicksProfilerTests {
  public static void ReportsHitsAndSamplesAndResets() {
    var profiler = new TicksProfiler();

    profiler.StartNewHit();
    profiler.Stop();
    profiler.NextFrame();

    var stats = profiler.GetStatsAndReset();

    Assert.True(stats.Contains("Samples: 1"), stats);
    Assert.True(stats.Contains("Hits: 1"), stats);
    Assert.True(stats.Contains("Avg:"), stats);
    Assert.True(stats.Contains("Max:"), stats);
    Assert.True(stats.Contains("Total:"), stats);
  }
}
