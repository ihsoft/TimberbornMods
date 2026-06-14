using System;
using System.Reflection;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.PowerConsumers;
using Timberborn.BlockingSystem;
using Timberborn.Buildings;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;

namespace SmartPower.Tests;

sealed class PowerInputLimiterHarness {
  public PowerInputLimiter Limiter { get; }
  public SmartPowerService Service { get; }
  public MechanicalGraph Graph { get; }
  public MechanicalNode Node { get; }
  public BlockableObject BlockableObject { get; }
  public StatusToggle Status { get; }
  public PausableBuilding PausableBuilding { get; }

  public PowerInputLimiterHarness(int nominalPower = 50) {
    Service = SmartPowerServiceFactory.Create(new FakeDayNightCycle { FixedDeltaTimeInHours = 0.25f });
    Graph = new MechanicalGraph();
    Node = new MechanicalNode { Graph = Graph, Active = true };
    Graph.Nodes.Add(Node);
    BlockableObject = new BlockableObject();
    Status = new StatusToggle();
    PausableBuilding = new PausableBuilding();
    Limiter = CreateLimiter(Service);
    Limiter.Automate = true;

    SetField("_mechanicalNode", Node);
    SetField("_blockableObject", BlockableObject);
    SetField("_pausableBuilding", PausableBuilding);
    SetField("_shutdownStatus", Status);
    SetField("_nominalPowerInput", nominalPower);
    SetField("_desiredPower", nominalPower);
    SetField("_suspendDelayedAction", Service.GetTickDelayedAction(0));
    SetField("_resumeDelayedAction", Service.GetTickDelayedAction(0));
  }

  public void SetAdjustablePowerInput(IAdjustablePowerInput adjustablePowerInput) {
    SetField("_adjustablePowerInput", adjustablePowerInput);
  }

  public void ForceSuspend() {
    Invoke("Suspend");
  }

  void SetField(string name, object value) {
    var field = typeof(PowerInputLimiter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    if (field == null) {
      throw new InvalidOperationException("Field not found: " + name);
    }
    field.SetValue(Limiter, value);
  }

  void Invoke(string name) {
    var method = typeof(PowerInputLimiter).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    if (method == null) {
      throw new InvalidOperationException("Method not found: " + name);
    }
    method.Invoke(Limiter, []);
  }

  static PowerInputLimiter CreateLimiter(SmartPowerService service) {
    var constructor = typeof(PowerInputLimiter).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(Timberborn.Localization.ILoc), typeof(SmartPowerService)],
        null);
    if (constructor == null) {
      throw new InvalidOperationException("PowerInputLimiter constructor was not found.");
    }
    return (PowerInputLimiter)constructor.Invoke([new FakeLoc(), service]);
  }
}

sealed class FakeAdjustablePowerInput : IAdjustablePowerInput {
  readonly int _powerInput;

  public int Calls { get; private set; }

  public FakeAdjustablePowerInput(int powerInput) {
    _powerInput = powerInput;
  }

  public int UpdateAndGetPowerInput() {
    Calls++;
    return _powerInput;
  }
}
