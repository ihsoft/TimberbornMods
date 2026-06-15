using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class CounterProfilerTests {
  public static void ReportsSortedFrameStatsAndResets() {
    var profiler = new CounterProfiler(1);

    profiler.Increment(3);
    profiler.NextFrame();
    profiler.Increment(1);
    profiler.NextFrame();
    profiler.Increment(5);
    profiler.NextFrame();

    Assert.Equal("Avg: 3; Mean: 3; Min: 1; Max: 5; Samples: 3", profiler.GetStatsAndReset());

    profiler.Increment(7);
    profiler.NextFrame();

    Assert.Equal("Avg: 7; Mean: 7; Min: 7; Max: 7; Samples: 1", profiler.GetStatsAndReset());
  }
}
