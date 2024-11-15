// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IgorZ.SmartPower.Settings;
using IgorZ.SmartPower.Utils;
using Timberborn.MechanicalSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.SmartPower.Core;

/// <summary>Service that manages the smart power consumption and production.</summary>
/// <remarks>
/// Consumers and generators that register themselves in the service will be dynamically paused/resumed to optimize
/// power production and consumption.
/// </remarks>
public class SmartPowerService : ITickableSingleton, ILateTickable {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    HandleSmartLogic();
    CurrentTick++;
    _networkCache.Clear();
  }

  #endregion

  #region API

  /// <summary>Gets the current tick number.</summary>
  public int CurrentTick { get; private set; }

  /// <summary>Tells if the smart logic can now start.</summary>
  public bool SmartLogicStarted => CurrentTick > 1;

  /// <summary>Gets the delayed action that will be executed after the specified number of ticks.</summary>
  public TickDelayedAction GetTickDelayedAction(int skipTicks) {
    return new TickDelayedAction(skipTicks, () => CurrentTick);
  }

  /// <summary>Gets the delayed action that will be executed after the specified number of game minutes.</summary>
  public TickDelayedAction GetTimeDelayedAction(int skipMinutes) {
    return new TickDelayedAction(Mathf.CeilToInt(skipMinutes / _fixedDeltaTimeInHours), () => CurrentTick);
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
      DebugEx.Fine("Power reservation updated: graph={0}", graph.GetHashCode(), reserved);
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

  /// <summary>Tells whether the generator is registered in the system.</summary>
  public bool IsGeneratorRegistered(ISuspendableGenerator generator) {
    return _allRegisteredGenerators.ContainsKey(generator) || _pendingGenerators.Contains(generator);
  }

  /// <summary>Tells whether the consumer is registered in the system.</summary>
  public bool IsConsumerRegistered(ISuspendableConsumer consumer) {
    return _allRegisteredConsumers.ContainsKey(consumer) || _pendingConsumers.Contains(consumer);
  }

  /// <summary>
  /// Registers the consumer in the service. If the generator is already registered, throws an error.
  /// </summary>
  public void RegisterConsumer(ISuspendableConsumer consumer) {
    if (_allRegisteredConsumers.ContainsKey(consumer) || _pendingConsumers.Contains(consumer)) {
      throw new InvalidOperationException("Consumer already registered: " + DebugEx.ObjectToString(consumer));
    }
    _pendingConsumers.Add(consumer);
    DebugEx.Fine("Schedule consumer registration: {0}", consumer);
  }

  /// <summary>Unregisters the consumer from the service. If the consumer is not registered, does nothing.</summary>
  public void UnregisterConsumer(ISuspendableConsumer consumer) {
    var unregistered = _pendingConsumers.Remove(consumer);
    if (_allRegisteredConsumers.TryGetValue(consumer, out var graph)) {
      _allRegisteredConsumers.Remove(consumer);
      var setup = _setupsPerGraph[graph];
      setup.AllConsumers.Remove(consumer);
      setup.ActiveConsumers.Remove(consumer);
      setup.SuspendedConsumers.Remove(consumer);
      CheckRemoveGraph(graph, setup);
      unregistered = true;
    }
    if (unregistered) {
      DebugEx.Fine("Unregistered consumer {0}", consumer);
    }
  }

  /// <summary>Updates service state to reflect the changed consumer index values.</summary>
  /// <seealso cref="ISuspendableConsumer.DesiredPower"/>
  public void UpdateConsumerOverrides(ISuspendableConsumer consumer) {
    if (!_allRegisteredConsumers.ContainsKey(consumer)) {
      return;
    }
    _changedConsumers.Add(consumer);
  }

  /// <summary>
  /// Registers the generator in the service. If the generator is already registered, throws an error.
  /// </summary>
  public void RegisterGenerator(ISuspendableGenerator generator) {
    if (_allRegisteredGenerators.ContainsKey(generator) || _pendingGenerators.Contains(generator)) {
      throw new InvalidOperationException("Generator already registered: " + DebugEx.ObjectToString(generator));
    }
    _pendingGenerators.Add(generator);
    DebugEx.Fine("Schedule generator registration: {0}", generator);
  }

  /// <summary>Unregisters the generator from the service. If the generator is not registered, does nothing.</summary>
  public void UnregisterGenerator(ISuspendableGenerator generator) {
    var unregistered = _pendingGenerators.Remove(generator);
    if (_allRegisteredGenerators.TryGetValue(generator, out var graph)) {
      _allRegisteredGenerators.Remove(generator);
      var setup = _setupsPerGraph[graph];
      setup.AllGenerators.Remove(generator);
      setup.ActiveGenerators.Remove(generator);
      setup.SpareGenerators.Remove(generator);
      CheckRemoveGraph(graph, setup);
      unregistered = true;
    }
    if (unregistered) {
      DebugEx.Fine("Unregistered generator {0}", generator);
    }
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

  /// <summary>
  /// Orders the generators so that the generators with the lowest priority and the lowest power output are first.
  /// </summary>
  /// <remarks>
  /// The idea is to activate generators from the end of the list (the highest priority) and suspend from the beginning
  /// of the list (the least priority).
  /// </remarks>
  sealed class GeneratorsComparerClass : IComparer<ISuspendableGenerator> {
    public int Compare(ISuspendableGenerator x, ISuspendableGenerator y) {
      if (x == null || y == null) {
        throw new ArgumentNullException("Generator is null: x=" + x + ", y=" + y);
      }
      var priorityCheck = x.Priority.CompareTo(y.Priority);
      if (priorityCheck != 0) {
        return priorityCheck;
      }
      var powerCheck = x.NominalOutput.CompareTo(y.NominalOutput);
      return powerCheck == 0
          ? string.Compare(x.StableUniqueId, y.StableUniqueId, StringComparison.Ordinal)
          : powerCheck;
    }
  }

  /// <summary>
  /// Orders the consumers so that the generators with the lowest priority and the highest power demand are first.
  /// </summary>
  /// <remarks>
  /// The idea is to activate consumers from the end of the list: they have high priority and demand less power, which
  /// allows activating more consumers. The consumers are suspended from the beginning of the list (the least priority)
  /// – it allows releasing more power with fewer buildings deactivated.
  /// </remarks>
  sealed class ConsumersComparerClass : IComparer<ISuspendableConsumer> {
    public int Compare(ISuspendableConsumer x, ISuspendableConsumer y) {
      if (x == null || y == null) {
        throw new ArgumentNullException("Consumer is null: x=" + x + ", y=" + y);
      }
      var priorityCheck = x.Priority.CompareTo(y.Priority);
      if (priorityCheck != 0) {
        return priorityCheck;
      }
      // Reverse check the power to have the highest power consumers disabled first.
      var powerCheck = y.DesiredPower.CompareTo(x.DesiredPower);
      return powerCheck == 0
          ? string.Compare(x.StableUniqueId, y.StableUniqueId, StringComparison.Ordinal)
          : powerCheck;
    }
  }

  static SmartPowerService _instance;
  static readonly GeneratorsComparerClass GeneratorsComparer = new();
  static readonly ConsumersComparerClass ConsumersComparer = new();

  struct GraphSetup {
    public List<ISuspendableGenerator> AllGenerators;  //FIXME: deperecate
    public List<ISuspendableGenerator> WarmingUpGenerators;
    public List<ISuspendableGenerator> ActiveGenerators;
    public List<ISuspendableGenerator> SpareGenerators;
    public List<ISuspendableConsumer> AllConsumers;  //FIXME: deperecate
    public List<ISuspendableConsumer> WarmingUpConsumers;
    public List<ISuspendableConsumer> ActiveConsumers;
    public List<ISuspendableConsumer> SuspendedConsumers;
  }

  readonly Dictionary<MechanicalGraph, GraphSetup> _setupsPerGraph = new();
  readonly HashSet<ISuspendableGenerator> _graphChangedGenerators = [];
  readonly HashSet<ISuspendableConsumer> _graphChangedConsumers = [];
  readonly List<ISuspendableConsumer> _changedConsumers = [];
  readonly Dictionary<ISuspendableGenerator, MechanicalGraph> _allRegisteredGenerators = new();
  readonly Dictionary<ISuspendableConsumer, MechanicalGraph> _allRegisteredConsumers = new();
  readonly List<ISuspendableGenerator> _pendingGenerators = [];
  readonly List<ISuspendableConsumer> _pendingConsumers = [];

  readonly float _fixedDeltaTimeInHours;

  int _skipUpdates = 2;  // How many updates after the game load to skip before starting the logic.

  SmartPowerService(IDayNightCycle dayNightCycle) {
    _instance = this;
    _fixedDeltaTimeInHours = dayNightCycle.FixedDeltaTimeInHours;
  }

  void HandleSmartLogic() {
    if (_skipUpdates > 0) {
      DebugEx.Fine("Skipping updates countdown: {0}", _skipUpdates);
      _skipUpdates--;
      return;
    }

    // Register pending generators.
    if (_pendingGenerators.Count > 0) {
      for (var i = _pendingGenerators.Count - 1; i >= 0; i--) {
        var generator = _pendingGenerators[i];
        var graph = generator.MechanicalNode.Graph;
        _allRegisteredGenerators[generator] = graph
            ?? throw new InvalidOperationException(
                "Generator is not connected to the graph: " + DebugEx.ObjectToString(generator));
        var setup = GetSetup(graph);
        setup.AllGenerators.Add(generator);
        if (generator.IsSuspended) {
          AddSortedGenerator(setup.SpareGenerators, generator);
        } else {
          AddSortedGenerator(setup.ActiveGenerators, generator);
          if (generator.MechanicalNode.PowerOutput == 0) {
            setup.WarmingUpGenerators.Add(generator);
          }
        }
        DebugEx.Fine(
            "Registered generator {0}: isSuspended={1}, powerOutput={2}", generator, generator.IsSuspended,
            generator.MechanicalNode.PowerOutput);
      }
      _pendingGenerators.Clear();
    }

    // Register pending consumers.
    if (_pendingConsumers.Count > 0) {
      for (var i = _pendingConsumers.Count - 1; i >= 0; i--) {
        var consumer = _pendingConsumers[i];
        var graph = consumer.MechanicalNode.Graph;
        _allRegisteredConsumers[consumer] = graph
            ?? throw new InvalidOperationException(
                "Consumer is not connected to the graph: " + DebugEx.ObjectToString(consumer));
        var setup = GetSetup(graph);
        setup.AllConsumers.Add(consumer);
        if (consumer.IsSuspended) {
          AddSortedConsumer(setup.SuspendedConsumers, consumer);
        } else {
          AddSortedConsumer(setup.ActiveConsumers, consumer);
          if (consumer.MechanicalNode.PowerInput == 0) {
            setup.WarmingUpConsumers.Add(consumer);
          }
        }
        DebugEx.Fine("Registered consumer {0}: isSuspended={1}", consumer, consumer.IsSuspended);
      }
      _pendingConsumers.Clear();
    }

    // Update sorted lists based on the new indexed values.
    if (_changedConsumers.Count > 0) {
      foreach (var consumer in _changedConsumers) {
        DebugEx.Fine("Consumer {0} index changed: isSuspended={1}, desiredPower={2}",
                     consumer, consumer.IsSuspended, consumer.DesiredPower);
        var setup = _setupsPerGraph[consumer.MechanicalNode.Graph];
        if (consumer.IsSuspended) {
          if (!setup.SuspendedConsumers.Remove(consumer)) {
            throw new InvalidDataException("Consumer is not in the suspended list: " + DebugEx.ObjectToString(consumer));
          }
          AddSortedConsumer(setup.SuspendedConsumers, consumer);
        } else {
          if (!setup.ActiveConsumers.Remove(consumer)) {
            throw new InvalidDataException("Consumer is not in the active list: " + DebugEx.ObjectToString(consumer));
          }
          AddSortedConsumer(setup.ActiveConsumers, consumer);
        }
      }
      _changedConsumers.Clear();
    }

    var batteryHysteresis = NetworkUISettings.BatteryRatioHysteresis;
    foreach (var pair in _setupsPerGraph) {
      BalanceNetwork(pair.Value, pair.Key, batteryHysteresis);
    }
  }

  void BalanceNetwork(GraphSetup setup, MechanicalGraph graph, float batteryHysteresis) {
    var batteryCapacity = 0f;
    var batteryCharge = 0f;
    var operationalBatteries = graph.BatteryControllers.Where(batteryCtrl => batteryCtrl.Operational);
    foreach (var batteryCtrl in operationalBatteries) {
      batteryCapacity += batteryCtrl.Capacity;
      batteryCharge += batteryCtrl.Charge;
    }
    var hasBatteries = batteryCapacity > 0;
    var batteryRatio = hasBatteries ? batteryCharge / batteryCapacity : 0;

    var activeGenerators = setup.ActiveGenerators;
    var spareGenerators = setup.SpareGenerators;
    var activeConsumers = setup.ActiveConsumers;
    var suspendedConsumers = setup.SuspendedConsumers;

    var powerSupply = (float) graph.CurrentPower.PowerSupply;
    for (var i = setup.WarmingUpGenerators.Count - 1; i >= 0; i--) {
      var generator = setup.WarmingUpGenerators[i];
      powerSupply += generator.NominalOutput;
      if (generator.MechanicalNode.PowerOutput > 0) {
        setup.WarmingUpGenerators.RemoveAt(i);
        DebugEx.Fine("Warming-up generator {0} is ready: nominalOutput={1}, currentOutput={2}",
                     generator, generator.NominalOutput, generator.MechanicalNode.PowerOutput);
      }
    }
    var powerDemand = (float) graph.CurrentPower.PowerDemand;
    for (var i = setup.WarmingUpConsumers.Count - 1; i >= 0; i--) {
      var consumer = setup.WarmingUpConsumers[i];
      powerDemand += consumer.DesiredPower;
      if (consumer.MechanicalNode.PowerInput > 0) {
        setup.WarmingUpConsumers.RemoveAt(i);
        DebugEx.Fine("Warming-up consumer {0} is ready: desiredPower={1}, currentPower={2}",
                     consumer, consumer.DesiredPower, consumer.MechanicalNode.PowerInput);
      }
    }

    if (hasBatteries) {
      foreach (var generator in setup.AllGenerators) {
        if (generator.IsSuspended) {
          if (batteryRatio <= generator.DischargeBatteriesThreshold) {
            ActivateGenerator(setup, generator);
          }
        } else {
          if (batteryRatio >= generator.ChargeBatteriesThreshold) {
            SuspendGenerator(setup, generator);
          }
        }
      }
    } else {
      if (spareGenerators.Count > 0 && powerSupply < powerDemand) {
        var generators = spareGenerators.ToArray();
        for (var i = generators.Length - 1; i >= 0 && powerSupply < powerDemand; i--) {
          var generator = generators[i];
          if (ActivateGenerator(setup, generator)) {
            powerSupply += generator.NominalOutput;
          }
        }
      }
    }

    if (suspendedConsumers.Count > 0) {
      var consumers = suspendedConsumers.ToArray();
      for (var i = consumers.Length - 1; i >= 0; i--) {
        var consumer = consumers[i];
        var newDemand = powerDemand + consumer.DesiredPower;

        // Try to achieve 100% efficiency for this consumer.
        if (!hasBatteries && newDemand > powerSupply + batteryCharge && spareGenerators.Count > 0) {
          var generators = spareGenerators.ToArray();
          for (var j = generators.Length - 1; j >= 0 && newDemand > powerSupply + batteryCharge; j--) {
            var generator = generators[j];
            if (ActivateGenerator(setup, generator)) {
              powerSupply += generator.NominalOutput;
            }
          }
        }

        // Try activating with what we have.
        var efficiency = powerSupply / newDemand;
        var newCharge = batteryCharge + (powerSupply - newDemand) * batteryHysteresis;
        if (efficiency < consumer.MinPowerEfficiency && (!hasBatteries || newCharge < float.Epsilon)) {
          continue;
        }
        if (hasBatteries && consumer.CheckBatteryCharge && newCharge / batteryCapacity < consumer.MinBatteriesCharge) {
          continue;
        }
        if (ActivateConsumer(setup, consumer)) {
          powerDemand += consumer.DesiredPower;
        }
      }
    }

    // Check if consumers need to stop.
    if (activeConsumers.Count > 0) {
      var consumers = activeConsumers.ToArray();
      // ReSharper disable once ForCanBeConvertedToForeach
      for (var i = 0; i < consumers.Length; i++) {
        var consumer = consumers[i];
        var nextTickCharge = batteryCharge + (powerSupply - powerDemand) * _fixedDeltaTimeInHours;
        var efficiency = (powerSupply + nextTickCharge) / powerDemand;
        var batteryLimit = hasBatteries && consumer.CheckBatteryCharge && batteryRatio < consumer.MinBatteriesCharge;
        if (nextTickCharge <= 0 && efficiency < consumer.MinPowerEfficiency || batteryLimit) {
          if (SuspendConsumer(setup, consumer, forceStop: batteryLimit)) {
            powerDemand -= consumer.DesiredPower;
          }
        }
      }
    }

    if (!hasBatteries && activeGenerators.Count > 0 && powerSupply > powerDemand) {
      var generators = activeGenerators.ToArray();
      // ReSharper disable once ForCanBeConvertedToForeach
      for (var i = 0; i < generators.Length; i++) {
        var generator = generators[i];
        if (powerSupply - generator.NominalOutput < powerDemand) {
          continue;
        }
        if (SuspendGenerator(setup, generator)) {
          powerSupply -= generator.NominalOutput;
        }
      }
    }
  }

  static bool ActivateGenerator(GraphSetup setup, ISuspendableGenerator generator) {
    DebugEx.Fine("Activating generator {0}: nominalOutput={1}", generator, generator.NominalOutput);
    generator.Resume();
    if (generator.IsSuspended) {
      DebugEx.Fine("Generator {0} refused to activate", generator);
      return false;
    }
    if (!setup.SpareGenerators.Remove(generator)) {
      throw new InvalidDataException("Generator is not in the spare list: " + DebugEx.ObjectToString(generator));
    }
    AddSortedGenerator(setup.ActiveGenerators, generator);
    if (generator.MechanicalNode.PowerOutput == 0) {
      setup.WarmingUpGenerators.Add(generator);
    }
    return true;
  }

  static bool SuspendGenerator(GraphSetup setup, ISuspendableGenerator generator) {
    DebugEx.Fine("Suspending generator {0}: nominalOutput={1}, currentOutput={2}",
                 generator, generator.NominalOutput, generator.MechanicalNode.PowerOutput);
    generator.Suspend(false);
    if (!generator.IsSuspended) {
      DebugEx.Fine("Generator {0} refused to suspend", generator);
      return false;
    }
    if (!setup.ActiveGenerators.Remove(generator)) {
      throw new InvalidDataException("Generator is not in the active list: " + DebugEx.ObjectToString(generator));
    }
    AddSortedGenerator(setup.SpareGenerators, generator);
    setup.WarmingUpGenerators.Remove(generator);
    return true;
  }

  static bool ActivateConsumer(GraphSetup setup, ISuspendableConsumer consumer) {
    DebugEx.Fine("Activating consumer {0}: desiredPower={1}", consumer, consumer.DesiredPower);
    consumer.Resume();
    if (consumer.IsSuspended) {
      DebugEx.Fine("Consumer {0} refused to activate", consumer);
      return false;
    }
    if (!setup.SuspendedConsumers.Remove(consumer)) {
      throw new InvalidDataException("Consumer is not in the suspended list: " + DebugEx.ObjectToString(consumer));
    }
    AddSortedConsumer(setup.ActiveConsumers, consumer);
    if (consumer.MechanicalNode.PowerInput == 0) {
      setup.WarmingUpConsumers.Add(consumer);
    }
    return true;
  }

  static bool SuspendConsumer(GraphSetup setup, ISuspendableConsumer consumer, bool forceStop = false) {
    DebugEx.Fine("Suspending consumer {0}: desiredPower={1}, currentInput={2}, forceStop={3}",
                 consumer, consumer.DesiredPower, consumer.MechanicalNode.PowerInput, forceStop);
    consumer.Suspend(forceStop);
    if (!consumer.IsSuspended) {
      DebugEx.Fine("Consumer {0} refused to suspend", consumer);
      return false;
    }
    if (!setup.ActiveConsumers.Remove(consumer)) {
      throw new InvalidDataException("Consumer is not in the active list: " + DebugEx.ObjectToString(consumer));
    }
    AddSortedConsumer(setup.SuspendedConsumers, consumer);
    setup.WarmingUpConsumers.Remove(consumer);
    return true;
  }

  void UpdateGeneratorGraph(ISuspendableGenerator generator) {
    if (_graphChangedGenerators.Remove(generator)) {
      DebugEx.Fine("Mechanical graph changed: generator={0}, newGraph={1}",
                   generator, generator.MechanicalNode.Graph.GetHashCode());
      RegisterGenerator(generator);
      return;
    }
    
    if (_allRegisteredGenerators.TryGetValue(generator, out var oldGraph)
        && generator.MechanicalNode.Graph != oldGraph) {
      UnregisterGenerator(generator);
      _graphChangedGenerators.Add(generator);
    }
  }

  void UpdateConsumerGraph(ISuspendableConsumer consumer) {
    if (_graphChangedConsumers.Contains(consumer)) {
      _graphChangedConsumers.Remove(consumer);
      DebugEx.Fine("Mechanical graph changed: consumer={0}, newGraph={1}",
                   consumer, consumer.MechanicalNode.Graph.GetHashCode());
      RegisterConsumer(consumer);
      return;
    }
    if (_allRegisteredConsumers.TryGetValue(consumer, out var oldGraph)
        && consumer.MechanicalNode.Graph != oldGraph) {
      UnregisterConsumer(consumer);
      _graphChangedConsumers.Add(consumer);
    }
  }

  GraphSetup GetSetup(MechanicalGraph graph) {
    if (_setupsPerGraph.TryGetValue(graph, out var setup)) {
      return setup;
    }
    setup = new GraphSetup {
        AllGenerators = [],
        WarmingUpGenerators = [],
        ActiveGenerators = [],
        SpareGenerators = [],
        AllConsumers = [],
        WarmingUpConsumers = [],
        ActiveConsumers = [],
        SuspendedConsumers = [],
    };
    _setupsPerGraph[graph] = setup;
    return setup;
  }

  void CheckRemoveGraph(MechanicalGraph graph, GraphSetup setup) {
    if (setup.AllGenerators.Count == 0 && setup.AllConsumers.Count == 0) {
      _setupsPerGraph.Remove(graph);
    }
  }

  static void AddSortedGenerator(List<ISuspendableGenerator> list, ISuspendableGenerator generator) {
    var index = list.BinarySearch(generator, GeneratorsComparer);
    if (index < 0) {
      index = ~index;
    } else {
      throw new InvalidOperationException("Generator already exists in the list: " + generator);
    }
    list.Insert(index, generator);
  }

  static void AddSortedConsumer(List<ISuspendableConsumer> list, ISuspendableConsumer cosumer) {
    var index = list.BinarySearch(cosumer, ConsumersComparer);
    if (index < 0) {
      index = ~index;
    } else {
      throw new InvalidOperationException("Consumer already exists in the list: " + cosumer);
    }
    list.Insert(index, cosumer);
  }

  #endregion

  #region Callbacks

  internal static void OnMechanicalNodeGraphChanged(MechanicalNode node) {
    var generator = node.GetComponentFast<ISuspendableGenerator>();
    if (generator != null) {
      _instance.UpdateGeneratorGraph(generator);
      return;
    }
    var consumer = node.GetComponentFast<ISuspendableConsumer>();
    if (consumer != null) {
      _instance.UpdateConsumerGraph(consumer);
    }
  }

  #endregion
}
