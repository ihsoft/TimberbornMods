// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.MechanicalSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.Core;

public class SmartPowerService : ITickableSingleton, ILateTickable {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    HandleSmartLogic();
  }

  #endregion

  #region API

  public void RegisterConsumer(ISuspendableConsumer consumer) {
    throw  new System.NotImplementedException();
  }

  public void UnregisterConsumer(ISuspendableConsumer consumer) {
    throw  new System.NotImplementedException();
  }

  /// <summary>
  /// Registers the generator in the service. If the generator is already registered, throws an error.
  /// </summary>
  public void RegisterGenerator(ISuspendableGenerator generator) {
    if (_allRegisteredGenerators.TryGetValue(generator, out var graph)) {
      if (graph != generator.MechanicalNode.Graph) {
        throw new System.InvalidOperationException("Generator already registered in another graph: " + generator);
      }
      return;
    }
    graph = generator.MechanicalNode.Graph;
    DebugEx.Fine(
      "Registering generator {0}, isSuspended={1}, graph={2}", generator, generator.IsSuspended, graph.GetHashCode());
    _allRegisteredGenerators[generator] = graph;
    if (!_setupsPerGraph.TryGetValue(graph, out var setup)) {
      setup = new GraphSetup {
          AllGenerators = [],
          ActiveGenerators = [],
          SpareGenerators = [],
          AllConsumers = [],
          ActiveConsumers = [],
          SuspendedConsumers = [],
      };
      DebugEx.Fine("Adding graph {0} to setups", graph.GetHashCode());
      _setupsPerGraph[graph] = setup;
    }
    setup.AllGenerators.Add(generator);
    AddSorted(generator.IsSuspended ? setup.SpareGenerators : setup.ActiveGenerators, generator);
  }

  /// <summary>Unregisters the generator from the service. If the generator is not registered, does nothing.</summary>
  public void UnregisterGenerator(ISuspendableGenerator generator) {
    if (!_allRegisteredGenerators.TryGetValue(generator, out var graph)) {
      return;
    }
    DebugEx.Fine("UnRegistering generator {0}, graph={1}", generator, graph.GetHashCode());
    _allRegisteredGenerators.Remove(generator);
    var setup = _setupsPerGraph[graph];
    setup.AllGenerators.Remove(generator);
    setup.ActiveGenerators.Remove(generator);
    setup.SpareGenerators.Remove(generator);
    if (setup.AllGenerators.Count == 0) {
      DebugEx.Fine("Removing graph {0} from setups", graph.GetHashCode());
      _setupsPerGraph.Remove(graph);
    }
  }

  #endregion

  #region Implementation

  List<ISuspendableGenerator> _allGenerators;
  List<ISuspendableGenerator> _activeGenerators;
  List<ISuspendableGenerator> _spareGenerators;
  List<ISuspendableConsumer> _allConsumers;
  List<ISuspendableConsumer> _activeConsumers;
  List<ISuspendableConsumer> _suspendedConsumers;

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
  readonly HashSet<ISuspendableGenerator> _dirtyGenerators = new();
  readonly Dictionary<ISuspendableGenerator, MechanicalGraph> _allRegisteredGenerators = new();

  SmartPowerService() {
    _instance = this;
  }

  void HandleSmartLogic() {
    foreach (var pair in _setupsPerGraph) {
      var graph = pair.Key;
      var setup = pair.Value;
      _allGenerators = setup.AllGenerators;
      _activeGenerators = setup.ActiveGenerators;
      _spareGenerators = setup.SpareGenerators;
      _allConsumers = setup.AllConsumers;
      _activeConsumers = setup.ActiveConsumers;
      _suspendedConsumers = setup.SuspendedConsumers;

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
        BalanceNetworkWithBatteries(batteryCharge / batteryCapacity);
      } else {
        BalanceNetworkWithoutBatteries(pair.Key);
      }
    }
  }

  void BalanceNetworkWithBatteries(float batteriesChargeRatio) {
    foreach (var generator in _allGenerators) {
      if (generator.IsSuspended) {
        if (batteriesChargeRatio <= generator.DischargeBatteriesThreshold) {
          ActivateGenerator(generator);
        }
      } else {
        if (batteriesChargeRatio >= generator.ChargeBatteriesThreshold) {
          SuspendGenerator(generator);
        }
      }
    }
    foreach (var consumer in _allConsumers) {
      if (consumer.IsSuspended) {
        if (batteriesChargeRatio >= consumer.MinBatteriesCharge) {
          //ActivateConsumer(consumer);
        }
      } else {
        if (batteriesChargeRatio <= consumer.MinBatteriesCharge) {
          //SuspendConsumer(consumer);
        }
      }
    }
  }

  void ActivateGenerator(ISuspendableGenerator generator) {
    _spareGenerators.Remove(generator);
    AddSorted(_activeGenerators, generator);
    generator.Resume();
    DebugEx.Fine("Activate generator {0}, power={1}", generator, generator.MechanicalNode.PowerOutput);
  }

  void SuspendGenerator(ISuspendableGenerator generator) {
    _activeGenerators.Remove(generator);
    AddSorted(_spareGenerators, generator);
    var power = generator.MechanicalNode.PowerOutput;
    generator.Suspend();
    DebugEx.Fine("Suspend generator {0}, power={1}", generator, power);
  }

  void ActivateConsumer() {
    var index = _suspendedConsumers.Count - 1;
    var consumer = _suspendedConsumers[index];
    _suspendedConsumers.RemoveAt(index);
    AddSorted(_activeConsumers, consumer);
    consumer.Resume();
    DebugEx.Fine("Activate consumer {0}, power={1}", consumer, consumer.MechanicalNode.PowerOutput);
  }

  void SuspendConsumer() {
    var consumer = _activeConsumers[0];
    _activeConsumers.RemoveAt(0);
    AddSorted(_suspendedConsumers, consumer);
    var power = consumer.MechanicalNode.PowerOutput;
    consumer.Suspend();
    DebugEx.Fine("Suspend consumer {0}, power={1}", consumer, power);
  }

  void BalanceNetworkWithoutBatteries(MechanicalGraph graph) {
    while (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && _spareGenerators.Count > 0) {
      ActivateGenerator(_spareGenerators[_spareGenerators.Count - 1]);
    }

    if (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && _spareGenerators.Count == 0) {
      while (graph.CurrentPower.PowerDemand > graph.CurrentPower.PowerSupply && _activeConsumers.Count > 0) {
        SuspendConsumer();
      }
    }

    if (graph.CurrentPower.PowerDemand < graph.CurrentPower.PowerSupply && _suspendedConsumers.Count == 0) {
      while (graph.CurrentPower.PowerDemand < graph.CurrentPower.PowerSupply && _activeGenerators.Count > 0) {
        var generator = _activeGenerators[0];
        var power = generator.MechanicalNode.PowerOutput;
        if (graph.CurrentPower.PowerSupply - power < graph.CurrentPower.PowerDemand) {
          break;
        }
        SuspendGenerator(generator);
      }
    }

    if (graph.CurrentPower.PowerDemand <= graph.CurrentPower.PowerSupply
        && _suspendedConsumers.Count > 0 && _spareGenerators.Count > 0) {
      var maximumSupply = graph.CurrentPower.PowerSupply
          + _spareGenerators.Sum(x => x.MechanicalNode._nominalPowerOutput);
      var desiredDemand = graph.CurrentPower.PowerDemand
          + _suspendedConsumers.Sum(x => x.DesiredPower);
      var skipConsumersCount = 0;
      while (desiredDemand > maximumSupply && skipConsumersCount < _suspendedConsumers.Count) {
        skipConsumersCount++;
        //FIXME: re-balance suspended collection by the current DesiredPower onchange.
        desiredDemand -= _suspendedConsumers[skipConsumersCount - 1].DesiredPower;
      }
      var skipGeneratorsCount = 0;
      while (desiredDemand < maximumSupply && skipGeneratorsCount < _spareGenerators.Count) {
        var power = _spareGenerators[skipGeneratorsCount].MechanicalNode._nominalPowerOutput;
        if (maximumSupply - power < desiredDemand) {
          break;
        }
        skipGeneratorsCount++;
        maximumSupply -= power;
      }
      while (_spareGenerators.Count >= skipGeneratorsCount) {
        ActivateGenerator(_spareGenerators[_spareGenerators.Count - 1]);
      }
      while (_suspendedConsumers.Count >= skipConsumersCount) {
        ActivateConsumer();
      }
    }
  }

  void UpdateGenerator(ISuspendableGenerator generator) {
    if (_dirtyGenerators.Contains(generator)) {
      HostedDebugLog.Fine(
        generator as BaseComponent, "Mechanical graph changed: {1}", generator.MechanicalNode.Graph.GetHashCode());
      RegisterGenerator(generator);
      _dirtyGenerators.Remove(generator);
      return;
    }
    
    if (_allRegisteredGenerators.TryGetValue(generator, out var oldGraph)
        && generator.MechanicalNode.Graph != oldGraph) {
      UnregisterGenerator(generator);
      _dirtyGenerators.Add(generator);
    }
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

  internal static void OnMechanicalNodeGraphChanged(MechanicalNode node) {
    var generator = node.GetComponentFast<ISuspendableGenerator>();
    if (generator != null) {
      _instance.UpdateGenerator(generator);
    }
  }

  #endregion
}
