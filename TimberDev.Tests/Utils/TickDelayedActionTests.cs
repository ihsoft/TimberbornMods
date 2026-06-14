using IgorZ.SmartPower.Utils;

namespace TimberDev.Tests;

static class TickDelayedActionTests {
  public static void ExecutesAfterSequentialTicks() {
    var tick = 0;
    var calls = 0;
    var action = new TickDelayedAction(2, () => tick);

    Assert.False(action.Execute(() => calls++));
    Assert.Equal(2, action.TicksLeft);

    tick = 1;
    Assert.False(action.Execute(() => calls++));
    Assert.Equal(1, action.TicksLeft);

    tick = 2;
    Assert.True(action.Execute(() => calls++));
    Assert.Equal(1, calls);
    Assert.Equal(-1, action.TicksLeft);
  }

  public static void RestartsAfterSkippedTick() {
    var tick = 0;
    var calls = 0;
    var action = new TickDelayedAction(2, () => tick);

    Assert.False(action.Execute(() => calls++));
    tick = 2;
    Assert.False(action.Execute(() => calls++));
    Assert.Equal(2, action.TicksLeft);
    Assert.Equal(0, calls);
  }

  public static void ForceExecutesImmediately() {
    var tick = 10;
    var calls = 0;
    var action = new TickDelayedAction(10, () => tick);

    Assert.True(action.Execute(() => calls++, force: true));
    Assert.Equal(1, calls);
    Assert.Equal(-1, action.TicksLeft);
  }
}
