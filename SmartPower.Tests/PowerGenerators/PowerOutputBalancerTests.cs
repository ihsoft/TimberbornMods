using IgorZ.SmartPower.Core;
using Timberborn.MechanicalSystem;

namespace SmartPower.Tests;

static class PowerOutputBalancerTests {
  public static void SuspendsChargedBatteries() {
    var harness = CreateHarness();
    harness.Graph.BatteryCapacity = 100;
    harness.Graph.BatteryCharge = 95;

    harness.Balancer.UpdateState();

    Assert.True(harness.Balancer.IsSuspended);
    Assert.True(harness.Status.Active);
  }

  public static void ResumesDischargedBatteries() {
    var harness = CreateHarness();
    harness.Graph.BatteryCapacity = 100;
    harness.Graph.BatteryCharge = 64;
    harness.Balancer.ForceSuspend();

    harness.Balancer.UpdateState();

    Assert.False(harness.Balancer.IsSuspended);
    Assert.False(harness.Status.Active);
  }

  public static void ResumesForReservedPowerDemand() {
    var harness = CreateHarness();
    harness.Graph.BatteryCapacity = 100;
    harness.Graph.BatteryCharge = 95;
    harness.Graph.PowerDemand = 80;
    harness.Graph.PowerSupply = 100;
    harness.Balancer.ForceSuspend();
    harness.Service.ReservePower(new MechanicalNode { Graph = harness.Graph }, 30);

    harness.Balancer.UpdateState();

    Assert.False(harness.Balancer.IsSuspended);
  }

  public static void SuspendsInactiveRedundantOutput() {
    var harness = CreateHarness();
    harness.Graph.PowerDemand = 40;
    harness.Graph.PowerSupply = 50;
    harness.Node.Actuals.PowerOutput = 0;

    harness.Balancer.UpdateState();

    Assert.True(harness.Balancer.IsSuspended);
  }

  public static void KeepsNeededOutputRunning() {
    var harness = CreateHarness();
    harness.Graph.PowerDemand = 70;
    harness.Graph.PowerSupply = 100;
    harness.Node.Actuals.PowerOutput = 50;

    harness.Balancer.UpdateState();

    Assert.False(harness.Balancer.IsSuspended);
  }

  public static void ResumesWhenAutomationDisabled() {
    var harness = CreateHarness();
    harness.Balancer.ForceSuspend();
    harness.Balancer.Automate = false;

    harness.Balancer.UpdateState();

    Assert.False(harness.Balancer.IsSuspended);
    Assert.Equal(1, harness.Balancer.AfterSmartLogicCalls);
  }

  public static void SkipsBalancingWhenDisabled() {
    var harness = CreateHarness();
    harness.Graph.BatteryCapacity = 100;
    harness.Graph.BatteryCharge = 95;
    harness.Balancer.OnExitFinishedState();

    harness.Balancer.UpdateState();

    Assert.False(harness.Balancer.IsSuspended);
    Assert.Equal(0, harness.Balancer.AfterSmartLogicCalls);
  }

  static PowerOutputBalancerHarness CreateHarness() {
    var graph = new MechanicalGraph();
    var node = new MechanicalNode { Graph = graph, _nominalPowerOutput = 50 };
    graph.Nodes.Add(node);

    var service = SmartPowerServiceFactory.Create(new FakeDayNightCycle { FixedDeltaTimeInHours = 0.25f });
    var balancer = new TestPowerOutputBalancer { Automate = true, Name = "TestBalancer" };
    var status = new Timberborn.StatusSystem.StatusToggle();
    balancer.Configure(node, service, new Timberborn.Buildings.PausableBuilding(), status);

    return new PowerOutputBalancerHarness(balancer, service, graph, node, status);
  }

  sealed record PowerOutputBalancerHarness(
      TestPowerOutputBalancer Balancer,
      SmartPowerService Service,
      MechanicalGraph Graph,
      MechanicalNode Node,
      Timberborn.StatusSystem.StatusToggle Status);
}
