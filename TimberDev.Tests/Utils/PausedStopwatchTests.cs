using System.Diagnostics;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class PausedStopwatchTests {
  public static void PausesRunningStopwatchAndResumesOnDispose() {
    var stopwatch = Stopwatch.StartNew();

    using (new PausedStopwatch(stopwatch)) {
      Assert.False(stopwatch.IsRunning);
    }

    Assert.True(stopwatch.IsRunning);
  }

  public static void StartsStoppedStopwatchOnDispose() {
    var stopwatch = new Stopwatch();

    using (new PausedStopwatch(stopwatch)) {
      Assert.False(stopwatch.IsRunning);
    }

    Assert.True(stopwatch.IsRunning);
  }
}
