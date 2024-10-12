using System.Collections.Generic;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core;

public interface ISuspendableConsumer : IComparer<ISuspendableConsumer> {
  public MechanicalNode MechanicalNode { get; }
  public int Priority { get; }
  public int DesiredPower { get; }
  public bool IsSuspended { get; }
  public float MinBatteriesCharge { get; }
  public void Suspend();
  public void Resume();
}
