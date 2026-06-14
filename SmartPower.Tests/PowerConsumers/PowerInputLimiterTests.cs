namespace SmartPower.Tests;

static class PowerInputLimiterTests {
  public static void SuspendsOnLowBatteries() {
    var harness = new PowerInputLimiterHarness();
    harness.Graph.BatteryCapacity = 100;
    harness.Graph.BatteryCharge = 20;
    harness.Limiter.CheckBatteryCharge = true;
    harness.Limiter.MinBatteriesCharge = 0.3f;

    harness.Limiter.UpdateState();

    Assert.True(harness.Limiter.IsSuspended);
    Assert.True(harness.Limiter.LowBatteriesCharge);
    Assert.True(harness.Status.Active);
    Assert.True(harness.BlockableObject.IsBlocked);
    Assert.Equal(50, harness.Service.GetReservedPower(harness.Graph));
  }

  public static void ResumesWhenAutomationDisabled() {
    var harness = new PowerInputLimiterHarness();
    harness.ForceSuspend();
    harness.Limiter.Automate = false;

    harness.Limiter.UpdateState();

    Assert.False(harness.Limiter.IsSuspended);
    Assert.False(harness.Status.Active);
    Assert.False(harness.BlockableObject.IsBlocked);
    Assert.Equal(0, harness.Service.GetReservedPower(harness.Graph));
  }

  public static void SuspendsOnLowEfficiencyWithoutBatteries() {
    var harness = new PowerInputLimiterHarness();
    harness.Graph.PowerEfficiency = 0.5f;
    harness.Limiter.MinPowerEfficiency = 0.9f;

    harness.Limiter.UpdateState();

    Assert.True(harness.Limiter.IsSuspended);
    Assert.True(harness.BlockableObject.IsBlocked);
  }

  public static void ResumesWhenSupplyRecovers() {
    var harness = new PowerInputLimiterHarness(20);
    harness.Graph.PowerDemand = 50;
    harness.Graph.PowerSupply = 100;
    harness.ForceSuspend();

    harness.Limiter.UpdateState();

    Assert.False(harness.Limiter.IsSuspended);
    Assert.False(harness.BlockableObject.IsBlocked);
  }

  public static void UpdatesAdjustablePowerInput() {
    var harness = new PowerInputLimiterHarness();
    var adjustablePowerInput = new FakeAdjustablePowerInput(7);
    harness.SetAdjustablePowerInput(adjustablePowerInput);
    harness.Limiter.Automate = false;

    harness.Limiter.UpdateState();

    Assert.Equal(1, adjustablePowerInput.Calls);
    Assert.Equal(7, harness.Node.Actuals.PowerInput);
  }
}
