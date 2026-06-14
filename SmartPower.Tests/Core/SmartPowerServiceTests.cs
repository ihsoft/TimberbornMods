using IgorZ.SmartPower.Core;
using Timberborn.MechanicalSystem;
using UnityEngine;

namespace SmartPower.Tests;

static class SmartPowerServiceTests {
  public static void TracksTicksAndStartupDelay() {
    var service = CreateService();

    Assert.Equal(0, service.CurrentTick);
    Assert.False(service.SmartLogicStarted);

    service.Tick();
    Assert.Equal(1, service.CurrentTick);
    Assert.False(service.SmartLogicStarted);

    service.Tick();
    Assert.Equal(2, service.CurrentTick);
    Assert.True(service.SmartLogicStarted);
  }

  public static void ReportsPausedGameState() {
    Time.timeScale = 1f;
    Assert.False(SmartPowerService.IsGamePaused);

    Time.timeScale = 0f;
    Assert.True(SmartPowerService.IsGamePaused);

    Time.timeScale = 1f;
  }

  public static void CachesFixedDeltaTimeInMinutes() {
    var dayNightCycle = new FakeDayNightCycle { FixedDeltaTimeInHours = 0.25f };
    var service = SmartPowerServiceFactory.Create(dayNightCycle);

    Assert.Equal(15f, service.FixedDeltaTimeInMinutes);

    dayNightCycle.FixedDeltaTimeInHours = 1f;
    Assert.Equal(15f, service.FixedDeltaTimeInMinutes);
  }

  public static void CreatesTickDelayedActions() {
    var service = CreateService();
    var calls = 0;
    var delayedAction = service.GetTickDelayedAction(2);

    Assert.False(delayedAction.Execute(() => calls++));
    service.Tick();
    Assert.False(delayedAction.Execute(() => calls++));
    service.Tick();
    Assert.True(delayedAction.Execute(() => calls++));
    Assert.Equal(1, calls);
  }

  public static void CreatesTimeDelayedActions() {
    var service = SmartPowerServiceFactory.Create(new FakeDayNightCycle { FixedDeltaTimeInHours = 0.25f });
    var calls = 0;
    var delayedAction = service.GetTimeDelayedAction(16);

    Assert.False(delayedAction.Execute(() => calls++));
    service.Tick();
    Assert.False(delayedAction.Execute(() => calls++));
    service.Tick();
    Assert.True(delayedAction.Execute(() => calls++));
    Assert.Equal(1, calls);
  }

  public static void SumsReservedPowerByGraph() {
    var service = CreateService();
    var graph = new MechanicalGraph();
    var otherGraph = new MechanicalGraph();

    service.ReservePower(new MechanicalNode { Graph = graph }, 10);
    service.ReservePower(new MechanicalNode { Graph = graph }, 15);
    service.ReservePower(new MechanicalNode { Graph = otherGraph }, 7);

    Assert.Equal(25, service.GetReservedPower(graph));
    Assert.Equal(7, service.GetReservedPower(otherGraph));
  }

  public static void UpdatesReservations() {
    var service = CreateService();
    var graph = new MechanicalGraph();
    var node = new MechanicalNode { Graph = graph };

    service.ReservePower(node, 10);
    Assert.Equal(10, service.GetReservedPower(graph));

    service.ReservePower(node, 20);
    Assert.Equal(20, service.GetReservedPower(graph));
  }

  public static void RemovesReservations() {
    var service = CreateService();
    var graph = new MechanicalGraph();
    var node = new MechanicalNode { Graph = graph };

    service.ReservePower(node, 10);
    service.ReservePower(node, -1);

    Assert.Equal(0, service.GetReservedPower(graph));
  }

  public static void AcceptsReservationsBeforeGraphInitialization() {
    var service = CreateService();
    var graph = new MechanicalGraph();
    var node = new MechanicalNode();

    service.ReservePower(node, 10);

    node.Graph = graph;
    Assert.Equal(10, service.GetReservedPower(graph));
  }

  static SmartPowerService CreateService() {
    return SmartPowerServiceFactory.Create(new FakeDayNightCycle { FixedDeltaTimeInHours = 0.25f });
  }
}
