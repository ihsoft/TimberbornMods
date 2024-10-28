// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core;

/// <summary>Interface for the generators that can be suspended and resumed.</summary>
public interface ISuspendableGenerator {
  /// <summary>The unique identifier of the generator that doesn't change between the game loads.</summary>
  public string StableUniqueId { get; }

  /// <summary>
  /// The priority in which the generator should be suspended and resumed. Generators with higher priority will be
  /// resumed first and suspended last.
  /// </summary>
  /// <remarks>This value mustn't change while the generator is under control of the smart power system.</remarks>
  public int Priority { get; }

  /// <summary>The mechanical node of this generator.</summary>
  public MechanicalNode MechanicalNode { get; }

  /// <summary>The nominal output of this generator if all conditions met.</summary>
  public int NominalOutput { get; }

  /// <summary>Indicates whether the generator is currently suspended.</summary>
  public bool IsSuspended { get; }

  /// <summary>The minimum level to let the batteries discharge to.</summary>
  public float DischargeBatteriesThreshold { get; }

  /// <summary>The maximum level to which this generator should charge the batteries.</summary>
  public float ChargeBatteriesThreshold { get; }

  /// <summary>Tells the generator to stop producing power.</summary>
  /// <param name="forceStop">Tells if generator can't refuse the shutdown and must stop.</param>
  public void Suspend(bool forceStop);

  /// <summary>Tells the generator to resume producing power.</summary>
  public void Resume();
}
