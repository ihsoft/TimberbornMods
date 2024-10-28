// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.MechanicalSystem;

namespace IgorZ.SmartPower.Core;

/// <summary>Interface for the consumers that can be suspended and resumed.</summary>
public interface ISuspendableConsumer {
  /// <summary>The unique identifier of the consumer that doesn't change between the game loads.</summary>
  public string StableUniqueId { get; }

  /// <summary>
  /// The priority in which the consumer should be suspended and resumed. Consumers with higher priority will be
  /// resumed first and suspended last.
  /// </summary>
  /// <remarks>This value mustn't change while the consumer is under control of SmartPowerSystem.</remarks>
  public int Priority { get; }

  /// <summary>The mechanical node of this generator.</summary>
  /// <remarks>This value mustn't change while the consumer is under control of SmartPowerSystem.</remarks>
  public MechanicalNode MechanicalNode { get; }

  /// <summary>Power that the consumer normally consumes.</summary>
  /// <remarks>
  /// If this value changes while the consumer is under control of SmartPowerService, then the service must be notified
  /// via <see cref="SmartPowerService.UpdateConsumerOverrides"/>.
  /// </remarks>
  /// <seealso cref="OverrideValues"/>
  public int DesiredPower { get; }

  /// <summary>Indicates whether the consumer is currently suspended.</summary>
  /// <remarks>The implementation of the interface is responsible for updating it.</remarks>
  /// <seaaalso cref="Suspend"/>
  /// <seealso cref="Resume"/>
  public bool IsSuspended { get; }

  /// <summary>The minimum network efficiency to keep the consumer working.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  public float MinPowerEfficiency { get; }

  /// <summary>Tells the consumer should suspend if batteries charge drops below the threshold.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  /// <seealso cref="MinBatteriesCharge"/>
  public bool CheckBatteryCharge { get; }

  /// <summary>The minimum level of the batteries to keep the consumer working.</summary>
  /// <remarks>This value can change in runtime without notifying SmartPowerSystem.</remarks>
  public float MinBatteriesCharge { get; }

  /// <summary>Tells the consumer to stop taking power and pause the production (if any).</summary>
  /// <remarks>
  /// The implementation of the interface is responsible for updating the suspended state. Unless
  /// <paramref name="forceStop"/> is set, this call is only a suggestion. The implementation can refuse it if it can't
  /// stop. To do so, don't set the <see cref="IsSuspended"/> to true.
  /// </remarks>
  /// <param name="forceStop">Tells if consumer can't refuse the shutdown and must stop.</param>
  /// <seealso cref="IsSuspended"/>
  public void Suspend(bool forceStop);

  /// <summary>Tells the generator to resume producing power and resume the consumption.</summary>
  /// <remarks>
  /// The implementation of the interface is responsible for updating the suspended state. This call is only a
  /// suggestion. The implementation can refuse it if it can't start. To do so, don't set the <see cref="IsSuspended"/>
  /// to false.
  /// </remarks>
  /// <seealso cref="IsSuspended"/>
  public void Resume();

  /// <summary>Replaces some consumer parameters with the new values.</summary>
  /// <remarks>
  /// <p>
  /// The consumer should "remember" its original settings. When asked to override, the new value must be used until
  /// it is canceled via an explicit call. However, the overrides shouldn't be persisted since they're assumed to be
  /// runtime only.
  /// </p>
  /// <p>
  /// The implementation should call <see cref="SmartPowerService.UpdateConsumerOverrides"/> to let service know
  /// that the value has changed.
  /// </p>
  /// </remarks>
  /// <param name="power">
  /// The new power consumption value. If set to zero, then the consumer must restore the original value.
  /// </param>
  public void OverrideValues(int? power=null);
}
