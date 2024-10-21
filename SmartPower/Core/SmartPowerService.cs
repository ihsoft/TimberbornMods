// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Timberborn.MechanicalSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;

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
  }

  #endregion

  #region API

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
      throw new System.InvalidOperationException("Consumer already registered: " + DebugEx.ObjectToString(consumer));
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

  /// <summary>
  /// Registers the generator in the service. If the generator is already registered, throws an error.
  /// </summary>
  public void RegisterGenerator(ISuspendableGenerator generator) {
    if (_allRegisteredGenerators.ContainsKey(generator) || _pendingGenerators.Contains(generator)) {
      throw new System.InvalidOperationException("Generator already registered: " + DebugEx.ObjectToString(generator));
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

  static SmartPowerService _instance;

  struct GraphSetup {
    public List<ISuspendableGenerator> AllGenerators;
    public List<ISuspendableGenerator> ActiveGenerators;
    public List<ISuspendableGenerator> SpareGenerators;
    public List<ISuspendableConsumer> AllConsumers;
    public List<ISuspendableConsumer> ActiveConsumers;
    public List<ISuspendableConsumer> SuspendedConsumers;
  }

  readonly Dictionary<MechanicalGraph, GraphSetup> _setupsPerGraph = new();
  readonly HashSet<ISuspendableGenerator> _dirtyGenerators = [];
  readonly HashSet<ISuspendableConsumer> _dirtyConsumers = [];
  readonly Dictionary<ISuspendableGenerator, MechanicalGraph> _allRegisteredGenerators = new();
  readonly Dictionary<ISuspendableConsumer, MechanicalGraph> _allRegisteredConsumers = new();
  readonly List<ISuspendableGenerator> _pendingGenerators = [];
  readonly List<ISuspendableConsumer> _pendingConsumers = [];

  int _skipUpdates = 2;  // How many updates after the game load to skip before starting the logic.

  SmartPowerService() {
    _instance = this;
  }

  void HandleSmartLogic() {
    if (_skipUpdates > 0) {
      DebugEx.Fine("Skipping updates countdown: {0}", _skipUpdates);
      _skipUpdates--;
      return;
    }
    RegisterPendingBuildings();

    foreach (var pair in _setupsPerGraph) {
      var graph = pair.Key;
      var setup = pair.Value;

      var batteryCapacity = 0;
      var batteryCharge = 0f;
      var hasBatteries = false;
      var operationalBatteries = graph.BatteryControllers.Where(batteryCtrl => batteryCtrl.Operational);
      foreach (var batteryCtrl in operationalBatteries) {
        batteryCapacity += batteryCtrl.Capacity;
        batteryCharge += batteryCtrl.Charge;
        hasBatteries = true;
      }
      if (hasBatteries) {
        BalanceNetworkWithBatteries(setup, batteryCharge / batteryCapacity);
      } else {
        BalanceNetworkWithoutBatteries(setup, graph);
      }
    }
  }

  static void BalanceNetworkWithBatteries(GraphSetup setup, float batteriesChargeRatio) {
    foreach (var generator in setup.AllGenerators) {
      if (generator.IsSuspended) {
        if (batteriesChargeRatio <= generator.DischargeBatteriesThreshold) {
          ActivateGenerator(setup, generator);
        }
      } else {
        if (batteriesChargeRatio >= generator.ChargeBatteriesThreshold) {
          SuspendGenerator(setup, generator);
        }
      }
    }
    foreach (var consumer in setup.AllConsumers) {
      if (consumer.IsSuspended) {
        if (batteriesChargeRatio >= consumer.MinBatteriesCharge) {
          ActivateConsumer(setup, consumer);
        }
      } else {
        if (batteriesChargeRatio <= consumer.MinBatteriesCharge) {
          SuspendConsumer(setup, consumer);
        }
      }
    }
  }

  static void ActivateGenerator(GraphSetup setup, ISuspendableGenerator generator) {
    setup.SpareGenerators.Remove(generator);
    AddSorted(setup.ActiveGenerators, generator);
    generator.Resume();
    DebugEx.Fine("Activate generator {0}, power={1}", generator, generator.MechanicalNode.PowerOutput);
  }

  static void SuspendGenerator(GraphSetup setup, ISuspendableGenerator generator) {
    setup.ActiveGenerators.Remove(generator);
    AddSorted(setup.SpareGenerators, generator);
    var power = generator.MechanicalNode.PowerOutput;
    generator.Suspend();
    DebugEx.Fine("Suspend generator {0}, power={1}", generator, power);
  }

  static void ActivateConsumer(GraphSetup setup, ISuspendableConsumer consumer) {
    setup.SuspendedConsumers.Remove(consumer);
    AddSorted(setup.ActiveConsumers, consumer);
    consumer.Resume();
    DebugEx.Fine("Activate consumer {0}: currentPower={1}, desiredPower={2}",
                 consumer, consumer.MechanicalNode.PowerOutput, consumer.DesiredPower);
  }

  static void SuspendConsumer(GraphSetup setup, ISuspendableConsumer consumer) {
    setup.ActiveConsumers.Remove(consumer);
    AddSorted(setup.SuspendedConsumers, consumer);
    var power = consumer.MechanicalNode.PowerInput;
    consumer.Suspend();
    DebugEx.Fine("Suspend consumer {0}, currentPower={1}, desiredPower={2}", consumer, power, consumer.DesiredPower);
  }

  static void BalanceNetworkWithoutBatteries(GraphSetup setup, MechanicalGraph graph) {
    var activeGenerators = setup.ActiveGenerators;
    var spareGenerators = setup.SpareGenerators;
    var activeConsumers = setup.ActiveConsumers;
    var suspendedConsumers = setup.SuspendedConsumers;

    while (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && spareGenerators.Count > 0) {
      ActivateGenerator(setup, spareGenerators[spareGenerators.Count - 1]);
    }

    if (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && spareGenerators.Count == 0) {
      while (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && activeConsumers.Count > 0) {
        SuspendConsumer(setup, activeConsumers[0]);
      }
    }

    if (graph.CurrentPower.PowerDemand < graph.CurrentPower.PowerSupply) {
      while (graph.CurrentPower.PowerDemand < graph.CurrentPower.PowerSupply && activeGenerators.Count > 0) {
        var generator = activeGenerators[0];
        var power = generator.MechanicalNode.PowerOutput;
        if (graph.CurrentPower.PowerSupply - power < graph.CurrentPower.PowerDemand) {
          break;
        }
        SuspendGenerator(setup, generator);
      }
    }

    if (graph.CurrentPower.PowerDemand <= graph.CurrentPower.PowerSupply && suspendedConsumers.Count > 0) {
      var maximumSupply = graph.CurrentPower.PowerSupply
          + spareGenerators.Sum(x => x.MechanicalNode._nominalPowerOutput);
      var totalDemand = graph.CurrentPower.PowerDemand
          + suspendedConsumers.Sum(x => x.DesiredPower);
      var skipConsumersCount = 0;
      while (totalDemand > maximumSupply && skipConsumersCount < suspendedConsumers.Count) {
        totalDemand -= suspendedConsumers[skipConsumersCount].DesiredPower;
        skipConsumersCount++;
      }
      var skipGeneratorsCount = 0;
      while (totalDemand < maximumSupply && skipGeneratorsCount < spareGenerators.Count) {
        var power = spareGenerators[skipGeneratorsCount].NominalOutput;
        if (maximumSupply - power < totalDemand) {
          break;
        }
        skipGeneratorsCount++;
        maximumSupply -= power;
      }
      while (spareGenerators.Count > skipGeneratorsCount) {
        ActivateGenerator(setup, spareGenerators[spareGenerators.Count - 1]);
      }
      while (suspendedConsumers.Count > skipConsumersCount) {
        ActivateConsumer(setup, suspendedConsumers[suspendedConsumers.Count - 1]);
      }
    }
    //FIXME: do re-balancing to fill unused power with lower priority consumers of small power.
  }

  void UpdateGenerator(ISuspendableGenerator generator) {
    if (_dirtyGenerators.Contains(generator)) {
      _dirtyGenerators.Remove(generator);
      DebugEx.Fine("Mechanical graph changed: generator={0}, newGraph={1}",
                   generator, generator.MechanicalNode.Graph.GetHashCode());
      RegisterGenerator(generator);
      return;
    }
    
    if (_allRegisteredGenerators.TryGetValue(generator, out var oldGraph)
        && generator.MechanicalNode.Graph != oldGraph) {
      UnregisterGenerator(generator);
      _dirtyGenerators.Add(generator);
    }
  }

  void UpdateConsumer(ISuspendableConsumer consumer) {
    if (_dirtyConsumers.Contains(consumer)) {
      _dirtyConsumers.Remove(consumer);
      DebugEx.Fine("Mechanical graph changed: consumer={0}, newGraph={1}",
                   consumer, consumer.MechanicalNode.Graph.GetHashCode());
      RegisterConsumer(consumer);
      return;
    }
    if (_allRegisteredConsumers.TryGetValue(consumer, out var oldGraph)
        && consumer.MechanicalNode.Graph != oldGraph) {
      UnregisterConsumer(consumer);
      _dirtyConsumers.Add(consumer);
    }
  }

  void RegisterPendingBuildings() {
    foreach (var generator in _pendingGenerators) {
      var graph = generator.MechanicalNode.Graph;
      if (graph == null) {
        throw new System.InvalidOperationException(
          "Generator is not connected to the graph: " + DebugEx.ObjectToString(generator));
      }
      _allRegisteredGenerators[generator] = graph;
      var setup = GetSetup(graph);
      setup.AllGenerators.Add(generator);
      AddSorted(generator.IsSuspended ? setup.SpareGenerators : setup.ActiveGenerators, generator);
      DebugEx.Fine("Registered generator {0}: isSuspended={1}", generator, generator.IsSuspended);
    }
    _pendingGenerators.Clear();

    foreach (var consumer in _pendingConsumers) {
      var graph = consumer.MechanicalNode.Graph;
      if (graph == null) {
        throw new System.InvalidOperationException(
          "Consumer is not connected to the graph: " + DebugEx.ObjectToString(consumer));
      }
      _allRegisteredConsumers[consumer] = graph;
      var setup = GetSetup(graph);
      setup.AllConsumers.Add(consumer);
      AddSorted(consumer.IsSuspended ? setup.SuspendedConsumers : setup.ActiveConsumers, consumer);
      DebugEx.Fine("Registered consumer {0}: isSuspended={1}", consumer, consumer.IsSuspended);
    }
    _pendingConsumers.Clear();
  }

  static void AddSorted<T>(List<T> list, T item) {
    var index = list.BinarySearch(item);
    if (index < 0) {
      index = ~index;
    } else {
      throw new System.InvalidOperationException("Item already exists in the list: " + item);
    }
    list.Insert(index, item);
  }

  static GraphSetup GetSetup(MechanicalGraph graph) {
    if (_instance._setupsPerGraph.TryGetValue(graph, out var setup)) {
      return setup;
    }
    setup = new GraphSetup {
        AllGenerators = [],
        ActiveGenerators = [],
        SpareGenerators = [],
        AllConsumers = [],
        ActiveConsumers = [],
        SuspendedConsumers = [],
    };
    _instance._setupsPerGraph[graph] = setup;
    return setup;
  }

  static void CheckRemoveGraph(MechanicalGraph graph, GraphSetup setup) {
    if (setup.AllGenerators.Count == 0 && setup.AllConsumers.Count == 0) {
      _instance._setupsPerGraph.Remove(graph);
    }
  }

  internal static void OnMechanicalNodeGraphChanged(MechanicalNode node) {
    var generator = node.GetComponentFast<ISuspendableGenerator>();
    if (generator != null) {
      _instance.UpdateGenerator(generator);
      return;
    }
    var consumer = node.GetComponentFast<ISuspendableConsumer>();
    if (consumer != null) {
      _instance.UpdateConsumer(consumer);
    }
  }

  #endregion
}
