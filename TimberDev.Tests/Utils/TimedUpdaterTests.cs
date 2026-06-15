using IgorZ.TimberDev.Utils;
using UnityEngine;

namespace TimberDev.Tests;

static class TimedUpdaterTests {
  public static void ExecutesOnlyAfterThreshold() {
    Time.unscaledTime = 0f;
    var calls = 0;
    var updater = new TimedUpdater(2f);

    updater.Update(() => calls++);
    Assert.Equal(0, calls);

    Time.unscaledTime = 1f;
    updater.Update(() => calls++);
    Assert.Equal(0, calls);

    Time.unscaledTime = 2f;
    updater.Update(() => calls++);
    Assert.Equal(1, calls);
  }

  public static void StartNowAndForceControlExecution() {
    Time.unscaledTime = 10f;
    var calls = 0;
    var updater = new TimedUpdater(2f, startNow: true);

    updater.Update(() => calls++);
    Assert.Equal(0, calls);

    updater.Update(() => calls++, force: true);
    Assert.Equal(1, calls);
  }
}
