// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core;

public interface ISuspendableConsumer : IComparable<ISuspendableConsumer> {
  /// <summary>
  /// The priority in which the consumer should be suspended and resumed. Consumers with higher priority will be
  /// resumed first and suspended last.
  /// </summary>
  /// <remarks>This value must not change while the consumer is under control of the smart power system.</remarks>
  public int Priority { get; }

  /// <summary>The mechanical node of this generator.</summary>
  public MechanicalNode MechanicalNode { get; }

  /// <summary>Power that the consumer normally consumes.</summary>
  public int DesiredPower { get; }

  /// <summary>Indicates whether the consumer is currently suspended.</summary>
  public bool IsSuspended { get; }

  /// <summary>The minimum level of the batteries to keep the consumer working.</summary>
  public float MinBatteriesCharge { get; }

  /// <summary>Tells the consumer to stop taking power and pause the production (if any).</summary>
  public void Suspend();

  /// <summary>Tells the generator to resume producing power and resume the consumption.</summary>
  public void Resume();
}
