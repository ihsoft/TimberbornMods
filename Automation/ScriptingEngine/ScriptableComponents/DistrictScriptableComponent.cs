// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.Bots;
using Timberborn.DwellingSystem;
using Timberborn.GameDistricts;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

class DistrictScriptableComponent : ScriptableComponentBase {

  const string BotPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Bots";
  const string BeaversPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Beavers";
  const string NumberOfBedsSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.NumberOfBeds";

  const string BotPopulationSignalName = "District.Bots";
  const string BeaverPopulationSignalName = "District.Beavers";
  const string NumberOfBedsSignalName = "District.NumberOfBeds";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "District";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      return [];
    }
    return [BeaverPopulationSignalName, BotPopulationSignalName, NumberOfBedsSignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError.BadStateError(building, "Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => () =>
            ScriptValue.FromInt(districtBuilding.District?.DistrictPopulation.Beavers.Count ?? 0),
        BotPopulationSignalName => () =>
            ScriptValue.FromInt(districtBuilding.District?.DistrictPopulation.NumberOfBots ?? 0),
        NumberOfBedsSignalName => () => {
          if (!districtBuilding.District) {
            return ScriptValue.FromInt(0);
          }
          var statistics =
              districtBuilding.District.GetComponentFast<DistrictDwellingStatisticsProvider>().GetDwellingStatistics();
          return ScriptValue.FromInt(statistics.FreeBeds + statistics.OccupiedBeds);
        },
        _ => throw new ScriptError.ParsingError("Unknown signal: " + name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError.BadStateError(building, "Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => BeaverPopulationSignalDef,
        BotPopulationSignalName => BotPopulationSignalDef,
        NumberOfBedsSignalName => NumberOfBedsSignalDef,
        _ => throw new ScriptError.ParsingError("Unknown signal: " + name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(string name, ISignalListener host) {
    var tracker = host.Behavior.GetComponentFast<DistrictChangeTracker>()
        ?? _instantiator.AddComponent<DistrictChangeTracker>(host.Behavior.GameObjectFast);
    var callback = new ScriptingService.SignalCallback(name, host);
    switch (name) {
      case BeaverPopulationSignalName:
        if (!tracker.OnBeaverPopulationChanged.Add(callback)) {
          throw new InvalidOperationException("Signal callback already registered: " + callback);
        }
        break;
      case BotPopulationSignalName:
        if (!tracker.OnBotPopulationChanged.Add(callback)) {
          throw new InvalidOperationException("Signal callback already registered: " + callback);
        }
        break;
      case NumberOfBedsSignalName:
        if (!tracker.OnDwellerCounterChanged.Add(callback)) {
          throw new InvalidOperationException("Signal callback already registered: " + callback);
        }
        break;
      default:
        throw new ScriptError.ParsingError("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(string name, ISignalListener host) {
    var callback = new ScriptingService.SignalCallback(name, host);
    var tracker = host.Behavior.GetComponentFast<DistrictChangeTracker>();
    if (!tracker) {
      DebugEx.Warning("Signal callback is not registered: {0}", callback);
      return;
    }
    if (!tracker.OnBeaverPopulationChanged.Remove(callback)
        && !tracker.OnBotPopulationChanged.Remove(callback)
        && !tracker.OnDwellerCounterChanged.Remove(callback)) {
      DebugEx.Warning("Signal callback is not registered: {0}", callback);
    }
  }

  #endregion

  #region Signals

  SignalDef BeaverPopulationSignalDef => _beaverPopulationSignalDef ??= new SignalDef {
      ScriptName = BeaverPopulationSignalName,
      DisplayName = Loc.T(BeaversPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
      },
  };
  SignalDef _beaverPopulationSignalDef;

  SignalDef BotPopulationSignalDef => _botPopulationSignalDef ??= new SignalDef {
      ScriptName = BotPopulationSignalName,
      DisplayName = Loc.T(BotPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
      },
  };
  SignalDef _botPopulationSignalDef;

  SignalDef NumberOfBedsSignalDef => _numberOfBedsSignalDef ??= new SignalDef {
      ScriptName = NumberOfBedsSignalName,
      DisplayName = Loc.T(NumberOfBedsSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
      },
  };
  SignalDef _numberOfBedsSignalDef;

  #endregion

  #region Implementation

  readonly BaseInstantiator _instantiator;

  DistrictScriptableComponent(BaseInstantiator instantiator) {
    _instantiator = instantiator;
  }

  #endregion

  #region District citizens tracker

  sealed class DistrictChangeTracker : BaseComponent {

    public readonly HashSet<ScriptingService.SignalCallback> OnBotPopulationChanged = [];
    public readonly HashSet<ScriptingService.SignalCallback> OnBeaverPopulationChanged = [];
    public readonly HashSet<ScriptingService.SignalCallback> OnDwellerCounterChanged = [];

    ScriptingService _scriptingService;
    DistrictCenter _currentDistrictCenter;

    [Inject]
    public void InjectDependencies(ScriptingService scriptingService) {
      _scriptingService = scriptingService;
    }

    void Start() {
      var districtBuilding = GetComponentFast<DistrictBuilding>();
      districtBuilding.ReassignedDistrict += OnDistrictChangedEvent;
      districtBuilding.ReassignedConstructionDistrict += OnDistrictChangedEvent;
      UpdateDistrictCenter();
    }

    void UpdateDistrictCenter() {
      if (_currentDistrictCenter) {
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingRegistered -= FinishedBuildingRegisteredEvent;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingUnregistered -= FinishedBuildingUnregisteredEvent;
      }
      _currentDistrictCenter = GetComponentFast<DistrictBuilding>().District;
      if (_currentDistrictCenter) {
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingRegistered += FinishedBuildingRegisteredEvent;
        _currentDistrictCenter.DistrictBuildingRegistry.FinishedBuildingUnregistered += FinishedBuildingUnregisteredEvent;
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
      if (citizen == null || citizen.GetComponentFast<BotSpec>()) {
        foreach (var callback in OnBotPopulationChanged) {
          _scriptingService.ScheduleSignalCallback(callback);
        }
      }
      if (citizen == null || !citizen.GetComponentFast<BotSpec>()) {
        foreach (var callback in OnBeaverPopulationChanged) {
          _scriptingService.ScheduleSignalCallback(callback);
        }
      }
    }

    void FinishedBuildingRegisteredEvent(object sender, FinishedBuildingRegisteredEventArgs arg) {
      OnDwellerCounterChangedEvent();
    }

    void FinishedBuildingUnregisteredEvent(object sender, FinishedBuildingUnregisteredEventArgs arg) {
      OnDwellerCounterChangedEvent();
    }

    void OnDwellerCounterChangedEvent() {
      foreach (var callback in OnDwellerCounterChanged) {
        _scriptingService.ScheduleSignalCallback(callback);
      }
    }
  }

  #endregion
}
