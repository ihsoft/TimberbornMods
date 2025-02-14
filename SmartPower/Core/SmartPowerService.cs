// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.SmartPower.Utils;
using Timberborn.MechanicalSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.Core;

/// <summary>Service that manages the smart power consumption and production.</summary>
/// <remarks>Consumers can signal that they need power to have more generators started.</remarks>
public class SmartPowerService : ITickableSingleton, ILateTickable {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    CurrentTick++;
    _networkCache.Clear();  // Must be refreshed at the beginning of each tick.
  }

  #endregion

  #region API

  /// <summary>Gets the current tick number.</summary>
  public int CurrentTick { get; private set; }

  /// <summary>Tells if the smart logic can now start.</summary>
  public bool SmartLogicStarted => CurrentTick > 1;

  /// <summary>Gets the fixed delta time in minutes.</summary>
  public float FixedDeltaTimeInMinutes => _fixedDeltaTimeInMinutes ??= _dayNightCycle.FixedDeltaTimeInHours * 60f;
  float? _fixedDeltaTimeInMinutes;

  /// <summary>Gets the delayed action that will be executed after the specified number of ticks.</summary>
  public TickDelayedAction GetTickDelayedAction(int skipTicks) {
    return new TickDelayedAction(skipTicks, () => CurrentTick);
  }

  /// <summary>Gets the delayed action that will be executed after the specified number of game minutes.</summary>
  public TickDelayedAction GetTimeDelayedAction(int skipMinutes) {
    return new TickDelayedAction(Mathf.CeilToInt(skipMinutes / FixedDeltaTimeInMinutes), () => CurrentTick);
  }

  /// <summary>Gets batteries total charge and capacity.</summary>
  public void GetBatteriesStat(MechanicalGraph graph, out int capacity, out float charge) {
    var paused = Time.timeScale < float.Epsilon;
    if (paused) {
      _networkCache.Clear();
    }
    if (!_networkCache.TryGetValue(graph, out var cache)) {
      cache = new NetworkCache();
      foreach (var batteryCtrl in graph.BatteryControllers.Where(x => x.Operational)) {
        cache.BatteriesCapacity += batteryCtrl.Capacity;
        cache.BatteriesCharge += batteryCtrl.Charge;
      }
      if (!paused) {
        _networkCache[graph] = cache;
      }
    }
    capacity = cache.BatteriesCapacity;
    charge = cache.BatteriesCharge;
  }

  /// <summary>Gets power reservation in the network.</summary>
  public int GetReservedPower(MechanicalGraph graph) {
    if (!_reservedPowerCache.TryGetValue(graph, out var reserved)) {
      reserved = _powerReservations.Where(x => x.Node.Graph == graph).Sum(x => x.Reserved);
      _reservedPowerCache.Add(graph, reserved);
      DebugEx.Fine("Graph power reservation updated: graph={0}, reserved={1}", graph.GetHashCode(), reserved);
    }
    return reserved;
  }

  /// <summary>Reserves power for the node.</summary>
  /// <param name="node">The node to reserve power for.</param>
  /// <param name="newReserved">The new amount of power to reserve. If negative, remove the reservation.</param>
  public void ReservePower(MechanicalNode node, int newReserved) {
    if (newReserved < 0) {
      DebugEx.Fine("Remove power reservation: node={0}", node);
      _powerReservations.RemoveAll(x => x.Node == node);
      _reservedPowerCache.Remove(node.Graph);
      return;
    }
    var reservation = _powerReservations.FirstOrDefault(x => x.Node == node);
    if (reservation == null) {
      DebugEx.Fine("Add power reservation: node={0}, reserved={1}", node, newReserved);
      reservation = new PowerReservation { Node = node, Reserved = newReserved };
      _powerReservations.Add(reservation);
    } else {
      DebugEx.Fine("Update power reservation: node={0}, oldReserved={1}, newReserved={2}",
                   node, reservation.Reserved, newReserved);
      reservation.Reserved = newReserved;
    }
    _reservedPowerCache.Remove(node.Graph);
  }

  #endregion

  #region Implementation

  struct NetworkCache {
    public int BatteriesCapacity;
    public float BatteriesCharge;
  }
  readonly Dictionary<MechanicalGraph, NetworkCache> _networkCache = [];

  class PowerReservation {
    public MechanicalNode Node;
    public int Reserved;
  }
  readonly List<PowerReservation> _powerReservations = [];

  readonly Dictionary<MechanicalGraph, int> _reservedPowerCache = [];

  readonly IDayNightCycle _dayNightCycle;

  SmartPowerService(IDayNightCycle dayNightCycle) {
    _dayNightCycle = dayNightCycle;
  }

  #endregion
}
