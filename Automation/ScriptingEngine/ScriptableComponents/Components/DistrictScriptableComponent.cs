// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.Bots;
using Timberborn.Common;
using Timberborn.DwellingSystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.ResourceCountingSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class DistrictScriptableComponent : ScriptableComponentBase, ITickableSingleton, ILateTickable {

  const string BotPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Bots";
  const string BeaversPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Beavers";
  const string NumberOfBedsSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.NumberOfBeds";
  const string ResourceCapacitySignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.ResourceCapacity";
  const string ResourceStockSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.ResourceStock";

  const string BotPopulationSignalName = "District.Bots";
  const string BeaverPopulationSignalName = "District.Beavers";
  const string NumberOfBedsSignalName = "District.NumberOfBeds";
  const string ResourceStockSignalNamePrefix = "District.ResourceStock.";
  const string ResourceCapacitySignalNamePrefix = "District.ResourceCapacity.";

  #region ITickableSingleton implemenation

  /// <inheritdoc/>
  public void Tick() {
    // The cost of this method will raise proportionally to the number of the rules, so keep it cheap. Optimize the code
    // and build indexes to avoid computations that can be made in the modifying methods and events.
    if (TrackersByDistrict.Count > 0) {
      foreach (var pair in TrackersByDistrict) {
        var resourceCounter = pair.Key.GetComponent<DistrictResourceCounter>();
        foreach (var tracker in pair.Value) {
          foreach (var goodId in tracker.GoodCapacity.Keys.ToArray()) { // Need a copy!
            var value = resourceCounter._capacityCounter.GetInputOutputCapacity(goodId);
            if (tracker.GoodCapacity[goodId] == value) {
              continue;
            }
            tracker.GoodCapacity[goodId] = value;
            tracker.ScheduleSignal(ResourceCapacitySignalNamePrefix + goodId, ignoreErrors: true);
          }
          foreach (var goodId in tracker.GoodStock.Keys.ToArray()) { // Need a copy!
            var value = resourceCounter._stockCounter.GetInputOutputStock(goodId)
                + resourceCounter._stockCounter.GetOutputStock(goodId);
            if (tracker.GoodStock[goodId] == value) {
              continue;
            }
            tracker.GoodStock[goodId] = value;
            tracker.ScheduleSignal(ResourceStockSignalNamePrefix + goodId, ignoreErrors: true);
          }
        }
      }
    }
    if (TrackersWithNoDistrict.Count > 0) {
      foreach (var tracker in TrackersWithNoDistrict) {
        foreach (var goodId in tracker.GoodCapacity.Keys.ToArray()) { // Need a copy!
          if (tracker.GoodCapacity[goodId] == 0) {
            continue;
          }
          tracker.GoodCapacity[goodId] = 0;
          tracker.ScheduleSignal(ResourceCapacitySignalNamePrefix + goodId, ignoreErrors: true);
        }
        foreach (var goodId in tracker.GoodStock.Keys.ToArray()) { // Need a copy!
          if (tracker.GoodStock[goodId] == 0) {
            continue;
          }
          tracker.GoodStock[goodId] = 0;
          tracker.ScheduleSignal(ResourceStockSignalNamePrefix + goodId, ignoreErrors: true);
        }
      }
    }
  }

  #endregion

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "District";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var districtBuilding = behavior.GetComponent<DistrictBuilding>();
    if (districtBuilding == null) {
      return [];
    }
    var res = new List<string> { BeaverPopulationSignalName, BotPopulationSignalName, NumberOfBedsSignalName };

    // District => finished, connected, and had at least one tick.
    // InstantDistrict => finished, connected, and on pause (updates instantly).
    // ConstructionDistrict => unfinished and connected.
    var districtCenter =
        districtBuilding.District ?? districtBuilding.InstantDistrict ?? districtBuilding.ConstructionDistrict;
    if (districtCenter != null) {
      var availableGoodIds = new HashSet<string>();
      var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
      availableGoodIds.AddRange(resourceCounter._stockCounter._inputOutputStock.Keys);
      availableGoodIds.AddRange(resourceCounter._stockCounter._outputStock.Keys);
      availableGoodIds.AddRange(resourceCounter._capacityCounter._inputOutputCapacity.Keys);
      availableGoodIds.AddRange(resourceCounter._capacityCounter._outputCapacity.Keys);
      var sortedGoodIds = availableGoodIds.OrderBy(x => _goodService.GetGoodOrNull(x).PluralDisplayName.Value);
      foreach (var goodId in sortedGoodIds) {
        res.Add(ResourceStockSignalNamePrefix + goodId);
        res.Add(ResourceCapacitySignalNamePrefix + goodId);
      }
    }

    return res.ToArray();
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var districtBuilding = GetComponentOrThrow<DistrictBuilding>(behavior);
    if (name.StartsWith(ResourceStockSignalNamePrefix)) {
      var goodId = ParseResourceSignalName(name).Id;
      return () => ResourceStockSignal(districtBuilding, goodId);
    }
    if (name.StartsWith(ResourceCapacitySignalNamePrefix)) {
      var goodId = ParseResourceSignalName(name).Id;
      return () => ResourceCapacitySignal(districtBuilding, goodId);
    }
    return name switch {
        BeaverPopulationSignalName => () => BeaverPopulationSignal(districtBuilding),
        BotPopulationSignalName => () => BotPopulationSignal(districtBuilding),
        NumberOfBedsSignalName => () => NumberOfBedsSignal(districtBuilding),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    GetComponentOrThrow<DistrictBuilding>(behavior);  // Just verify.
    if (name.StartsWith(ResourceStockSignalNamePrefix)) {
      return _signalDefsCache.GetOrAdd(name, MakeResourceStockTrackerSignalDef);
    }
    if (name.StartsWith(ResourceCapacitySignalNamePrefix)) {
      return _signalDefsCache.GetOrAdd(name, MakeResourceCapacityTrackerSignalDef);
    }
    return name switch {
        BeaverPopulationSignalName => BeaverPopulationSignalDef,
        BotPopulationSignalName => BotPopulationSignalDef,
        NumberOfBedsSignalName => NumberOfBedsSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }
  readonly ObjectsCache<SignalDef> _signalDefsCache = new();

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name.StartsWith(ResourceStockSignalNamePrefix) || name.StartsWith(ResourceCapacitySignalNamePrefix)
        || name is BeaverPopulationSignalName or BotPopulationSignalName or NumberOfBedsSignalName) {
      host.Behavior.GetOrCreate<DistrictChangeTracker>().AddSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name.StartsWith(ResourceStockSignalNamePrefix) || name.StartsWith(ResourceCapacitySignalNamePrefix)
        || name is BeaverPopulationSignalName or BotPopulationSignalName or NumberOfBedsSignalName) {
      host.Behavior.GetOrThrow<DistrictChangeTracker>().RemoveSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  #endregion

  #region Signals

  SignalDef BeaverPopulationSignalDef => _beaverPopulationSignalDef ??= new SignalDef {
      ScriptName = BeaverPopulationSignalName,
      DisplayName = Loc.T(BeaversPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _beaverPopulationSignalDef;

  SignalDef BotPopulationSignalDef => _botPopulationSignalDef ??= new SignalDef {
      ScriptName = BotPopulationSignalName,
      DisplayName = Loc.T(BotPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _botPopulationSignalDef;

  SignalDef NumberOfBedsSignalDef => _numberOfBedsSignalDef ??= new SignalDef {
      ScriptName = NumberOfBedsSignalName,
      DisplayName = Loc.T(NumberOfBedsSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _numberOfBedsSignalDef;

  SignalDef MakeResourceStockTrackerSignalDef(string signalName) {
    var spec = ParseResourceSignalName(signalName);
    return new SignalDef {
        ScriptName = signalName,
        DisplayName = Loc.T(ResourceStockSignalLocKey, spec.PluralDisplayName.Value),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
            DisplayNumericFormatRange = (0, float.NaN),
        },
    };
  }

  SignalDef MakeResourceCapacityTrackerSignalDef(string signalName) {
    var spec = ParseResourceSignalName(signalName);
    return new SignalDef {
        ScriptName = signalName,
        DisplayName = Loc.T(ResourceCapacitySignalLocKey, spec.PluralDisplayName.Value),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
            DisplayNumericFormatRange = (0, float.NaN),
        },
    };
  }

  ScriptValue ResourceStockSignal(DistrictBuilding districtBuilding, string goodId) {
    var districtCenter = districtBuilding.District;
    if (!districtCenter) { // Disconnected buildings don't have District.
      return ScriptValue.FromInt(0);
    }
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    return ScriptValue.FromInt(resourceCounter.GetResourceCount(goodId).AvailableStock);
  }

  ScriptValue ResourceCapacitySignal(DistrictBuilding districtBuilding, string goodId) {
    var districtCenter = districtBuilding.District;
    if (!districtCenter) { // Disconnected buildings don't have District.
      return ScriptValue.FromInt(0);
    }
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    return ScriptValue.FromInt(resourceCounter.GetResourceCount(goodId).InputOutputCapacity);
  }

  static ScriptValue BeaverPopulationSignal(DistrictBuilding districtBuilding) {
    return ScriptValue.FromInt(districtBuilding.District?.DistrictPopulation.Beavers.Count ?? 0);
  }

  static ScriptValue BotPopulationSignal(DistrictBuilding districtBuilding) {
    return ScriptValue.FromInt(districtBuilding.District?.DistrictPopulation.Bots.Count ?? 0);
  }

  static ScriptValue NumberOfBedsSignal(DistrictBuilding districtBuilding) {
    if (!districtBuilding.District) {
      return ScriptValue.FromInt(0);
    }
    var statistics =
        districtBuilding.District.GetComponent<DistrictDwellingStatisticsProvider>().GetDwellingStatistics();
    return ScriptValue.FromInt(statistics.FreeBeds + statistics.OccupiedBeds);
  }

  #endregion

  #region Implementation

  readonly IGoodService _goodService;

  static readonly Dictionary<DistrictCenter, HashSet<DistrictChangeTracker>> TrackersByDistrict = new ();
  static readonly HashSet<DistrictChangeTracker> TrackersWithNoDistrict = [];

  DistrictScriptableComponent(IGoodService goodService) {
    _goodService = goodService;
    TrackersByDistrict.Clear();
    TrackersWithNoDistrict.Clear();
  }

  GoodSpec ParseResourceSignalName(string signalName) {
    var parts = signalName.Split('.', 3);
    if (parts.Length != 3) { // Callers must check it!
      throw new InvalidOperationException($"Malformed resource signal name: {signalName}");
    }
    var goodSpec = _goodService.GetGoodOrNull(parts[2]);
    if (goodSpec == null) {
      throw new ScriptError.ParsingError("Unknown resource name: " + parts[2]);
    }
    return goodSpec;
  }

  #endregion

  #region District citizens tracker

  internal sealed class DistrictChangeTracker : AbstractStatusTracker {

    #region AbstractStatusTracker overrides

    /// <inheritdoc/>
    public override void Start() {
      base.Start();
      var districtBuilding = AutomationBehavior.GetComponentOrFail<DistrictBuilding>();
      districtBuilding.ReassignedDistrict += OnDistrictChangedEvent;
      districtBuilding.ReassignedConstructionDistrict += OnDistrictChangedEvent;
      UpdateDistrictCenter();
    }

    /// <inheritdoc/>
    public override void AddSignal(SignalOperator signalOperator, ISignalListener host) {
      base.AddSignal(signalOperator, host);
      var signalName = signalOperator.SignalName;
      var district = AutomationBehavior.GetComponentOrFail<DistrictBuilding>().District;
      var resourceCounter = district?.GetComponentInChildren<DistrictResourceCounter>();
      if (signalName.StartsWith(ResourceCapacitySignalNamePrefix)) {
        var goodId = signalName[ResourceCapacitySignalNamePrefix.Length..];
        if (!GoodCapacity.ContainsKey(goodId)) {
          var value = resourceCounter != null ? resourceCounter.GetResourceCount(goodId).InputOutputCapacity : 0;
          GoodCapacity.Add(goodId, value);
          HostedDebugLog.Fine(AutomationBehavior, "Start tracking district signal: {0}, value={1}", signalName, value);
        }
      } else if (signalName.StartsWith(ResourceStockSignalNamePrefix)) {
        var goodId = signalName[ResourceStockSignalNamePrefix.Length..];
        if (!GoodStock.ContainsKey(goodId)) {
          var value = resourceCounter != null ? resourceCounter.GetResourceCount(goodId).AvailableStock : 0;
          GoodStock.Add(goodId, value);
          HostedDebugLog.Fine(AutomationBehavior, "Start tracking district signal: {0}, value={1}", signalName, value);
        }
      }
    }

    /// <inheritdoc/>
    public override void RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
      base.RemoveSignal(signalOperator, host);
      var signalName = signalOperator.SignalName;
      if (!signalName.StartsWith(ResourceCapacitySignalNamePrefix)) {
        return;
      }
      var hasAnySuchSignal = ReferenceManager.Signals.SelectMany(x => x.Value).Any(x => x.SignalName == signalName);
      if (hasAnySuchSignal) {
        return;  // Still need to track it.
      }
      var goodId = signalName[ResourceCapacitySignalNamePrefix.Length..];
      HostedDebugLog.Fine(AutomationBehavior, "Stop tracking district signal: {0}", signalName);
      if (!GoodCapacity.Remove(goodId)) { // It's an abnormal situation.
        throw new InvalidOperationException($"Cannot remove resource capacity for: {signalName}");
      }
    }

    #endregion

    #region Implementation

    DistrictCenter _currentDistrictCenter;

    public readonly Dictionary<string, int> GoodCapacity = [];
    public readonly Dictionary<string, int> GoodStock = [];

    void UpdateDistrictCenter() {
      if (_currentDistrictCenter) {
        if (TrackersByDistrict.TryGetValue(_currentDistrictCenter, out var trackers)) {
          trackers.Remove(this);
          if (trackers.Count == 0) {
            TrackersByDistrict.Remove(_currentDistrictCenter);
          }
        }
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingRegistered -= FinishedBuildingRegisteredEvent;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingUnregistered -= FinishedBuildingUnregisteredEvent;
      }
      _currentDistrictCenter = AutomationBehavior.GetComponentOrFail<DistrictBuilding>().District;
      if (_currentDistrictCenter) {
        TrackersWithNoDistrict.Remove(this);
        TrackersByDistrict.GetOrAdd(_currentDistrictCenter, () => []).Add(this);
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingRegistered += FinishedBuildingRegisteredEvent;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingUnregistered += FinishedBuildingUnregisteredEvent;
      } else {
        TrackersWithNoDistrict.Add(this);
      }
    }

    void OnDistrictChangedEvent(object obj, EventArgs args) {
      UpdateDistrictCenter();
      OnPopulationChangedEvent();
    }

    void OnCitizenAssigned(object sender, CitizenAssignedEventArgs args) {
      OnPopulationChangedEvent(args.Citizen);
    }

    void OnCitizenUnassigned(object sender, CitizenUnassignedEventArgs args) {
      OnPopulationChangedEvent(args.Citizen);
    }

    void OnPopulationChangedEvent(Citizen citizen = null) {
      if (citizen == null || citizen.GetComponent<BotSpec>() != null) {
        ScheduleSignal(BotPopulationSignalName, ignoreErrors: true);
      }
      if (citizen == null || citizen.GetComponent<BotSpec>() == null) {
        ScheduleSignal(BeaverPopulationSignalName, ignoreErrors: true);
      }
    }

    void FinishedBuildingRegisteredEvent(object sender, FinishedBuildingRegisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
    }

    void FinishedBuildingUnregisteredEvent(object sender, FinishedBuildingUnregisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
    }

    #endregion
  }

  #endregion
}
