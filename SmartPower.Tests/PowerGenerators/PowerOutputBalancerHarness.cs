using System;
using System.Reflection;
using IgorZ.SmartPower.Core;
using IgorZ.SmartPower.PowerGenerators;
using IgorZ.SmartPower.Utils;
using Timberborn.Buildings;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;

namespace SmartPower.Tests;

sealed class TestPowerOutputBalancer : PowerOutputBalancer {
  protected override bool CanBeAutomated => CanAutomate;

  public bool CanAutomate { get; set; } = true;
  public int AfterSmartLogicCalls { get; private set; }

  public void Configure(
      MechanicalNode mechanicalNode,
      SmartPowerService smartPowerService,
      PausableBuilding pausableBuilding,
      StatusToggle statusToggle,
      TickDelayedAction suspendDelayedAction = null,
      TickDelayedAction resumeDelayedAction = null) {
    SetBackingField("MechanicalNode", mechanicalNode);
    SetBackingField("SmartPowerService", smartPowerService);
    SetField("_pausableBuilding", pausableBuilding);
    SetField("_shutdownStatus", statusToggle);
    SuspendDelayedAction = suspendDelayedAction ?? smartPowerService.GetTickDelayedAction(0);
    ResumeDelayedAction = resumeDelayedAction ?? smartPowerService.GetTickDelayedAction(0);
  }

  public void ForceSuspend() {
    Suspend();
  }

  protected override void OnAfterSmartLogic() {
    AfterSmartLogicCalls++;
  }

  void SetBackingField(string propertyName, object value) {
    SetField($"<{propertyName}>k__BackingField", value);
  }

  void SetField(string name, object value) {
    var field = typeof(PowerOutputBalancer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    if (field == null) {
      throw new InvalidOperationException("Field not found: " + name);
    }
    field.SetValue(this, value);
  }
}
