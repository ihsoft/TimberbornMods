// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.Bots;
using Timberborn.DwellingSystem;
using Timberborn.GameDistricts;
using Timberborn.Population;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class DistrictScriptableComponent : ScriptableComponentBase {

  const string BotPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Bots";
  const string BeaversPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Beavers";
  const string NumberOfBedsSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.NumberOfBeds";
  const string UnemployedBeaversSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.UnemployedBeavers";
  const string UnemployedBotsSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.UnemployedBots";

  const string BotPopulationSignalName = "District.Bots";
  const string BeaverPopulationSignalName = "District.Beavers";
  const string NumberOfBedsSignalName = "District.NumberOfBeds";
  const string UnemployedBeaversSignalName = "District.UnemployedBeavers";
  const string UnemployedBotsSignalName = "District.UnemployedBots";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "District";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponentFast<DistrictBuilding>()
        ? [BeaverPopulationSignalName, BotPopulationSignalName, NumberOfBedsSignalName,
           UnemployedBeaversSignalName, UnemployedBotsSignalName]
        : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var districtBuilding = behavior.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError.BadStateError(behavior, "Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => () => BeaverPopulationSignal(districtBuilding),
        BotPopulationSignalName => () => BotPopulationSignal(districtBuilding),
        NumberOfBedsSignalName => () => NumberOfBedsSignal(districtBuilding),
        UnemployedBeaversSignalName => () => UnemployedBeaversSignal(districtBuilding),
        UnemployedBotsSignalName => () => UnemployedBotsSignal(districtBuilding),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var districtBuilding = behavior.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError.BadStateError(behavior, "Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => BeaverPopulationSignalDef,
        BotPopulationSignalName => BotPopulationSignalDef,
        NumberOfBedsSignalName => NumberOfBedsSignalDef,
        UnemployedBeaversSignalName => UnemployedBeaversSignalDef,
        UnemployedBotsSignalName => UnemployedBotsSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name is not (BeaverPopulationSignalName or BotPopulationSignalName or NumberOfBedsSignalName
        or UnemployedBeaversSignalName or UnemployedBotsSignalName)) {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
    host.Behavior.GetOrCreate<DistrictChangeTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<DistrictChangeTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef BeaverPopulationSignalDef => _beaverPopulationSignalDef ??= new SignalDef {
      ScriptName = BeaverPopulationSignalName,
      DisplayName = Loc.T(BeaversPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
      },
  };
  SignalDef _beaverPopulationSignalDef;

  SignalDef BotPopulationSignalDef => _botPopulationSignalDef ??= new SignalDef {
      ScriptName = BotPopulationSignalName,
      DisplayName = Loc.T(BotPopulationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
      },
  };
  SignalDef _botPopulationSignalDef;

  SignalDef NumberOfBedsSignalDef => _numberOfBedsSignalDef ??= new SignalDef {
      ScriptName = NumberOfBedsSignalName,
      DisplayName = Loc.T(NumberOfBedsSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
      },
  };
  SignalDef _numberOfBedsSignalDef;

  SignalDef UnemployedBeaversSignalDef => _unemployedBeaversSignalDef ??= new SignalDef {
      ScriptName = UnemployedBeaversSignalName,
      DisplayName = Loc.T(UnemployedBeaversSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
      },
  };
  SignalDef _unemployedBeaversSignalDef;

  SignalDef UnemployedBotsSignalDef => _unemployedBotsSignalDef ??= new SignalDef {
      ScriptName = UnemployedBotsSignalName,
      DisplayName = Loc.T(UnemployedBotsSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueValidator = ValueDef.RangeCheckValidatorInt(min: 0),
      },
  };
  SignalDef _unemployedBotsSignalDef;

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
        districtBuilding.District.GetComponentFast<DistrictDwellingStatisticsProvider>().GetDwellingStatistics();
    return ScriptValue.FromInt(statistics.FreeBeds + statistics.OccupiedBeds);
  }

  static ScriptValue UnemployedBeaversSignal(DistrictBuilding districtBuilding) {
    var district = districtBuilding.District;
    if (!district) {
      return ScriptValue.FromInt(0);
    }
    PopDataCollector.CollectData(district, PopData);
    return ScriptValue.FromInt(PopData.BeaverWorkplaceData.Unemployed);
  }

  static ScriptValue UnemployedBotsSignal(DistrictBuilding districtBuilding) {
    var district = districtBuilding.District;
    if (!district) {
      return ScriptValue.FromInt(0);
    }
    PopDataCollector.CollectData(district, PopData);
    return ScriptValue.FromInt(PopData.BotWorkplaceData.Unemployed);
  }

  static readonly PopulationDataCollector PopDataCollector = new();
  static readonly PopulationData PopData = new();

  #endregion

  #region Implementation

  readonly BaseInstantiator _instantiator;

  DistrictScriptableComponent(BaseInstantiator instantiator) {
    _instantiator = instantiator;
  }

  #endregion

  #region District citizens tracker

  sealed class DistrictChangeTracker : AbstractStatusTracker {

    DistrictCenter _currentDistrictCenter;
    readonly List<Workplace> _trackedWorkplaces = new();

    void Start() {
      var districtBuilding = GetComponentFast<DistrictBuilding>();
      districtBuilding.ReassignedDistrict += OnDistrictChangedEvent;
      districtBuilding.ReassignedConstructionDistrict += OnDistrictChangedEvent;
      UpdateDistrictCenter();
    }

    void UpdateDistrictCenter() {
      UnsubscribeFromWorkplaces();
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
        SubscribeToWorkplaces();
      }
    }

    void SubscribeToWorkplaces() {
      foreach (var workplace in _currentDistrictCenter.DistrictBuildingRegistry.GetEnabledBuildings<Workplace>()) {
        workplace.WorkerAssigned += OnWorkerAssignmentChanged;
        workplace.WorkerUnassigned += OnWorkerAssignmentChanged;
        _trackedWorkplaces.Add(workplace);
      }
    }

    void UnsubscribeFromWorkplaces() {
      foreach (var workplace in _trackedWorkplaces) {
        if (workplace) {
          workplace.WorkerAssigned -= OnWorkerAssignmentChanged;
          workplace.WorkerUnassigned -= OnWorkerAssignmentChanged;
        }
      }
      _trackedWorkplaces.Clear();
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
      if (!citizen || citizen.GetComponentFast<BotSpec>()) {
        ScheduleSignal(BotPopulationSignalName, ignoreErrors: true);
        ScheduleSignal(UnemployedBotsSignalName, ignoreErrors: true);
      }
      if (!citizen || !citizen.GetComponentFast<BotSpec>()) {
        ScheduleSignal(BeaverPopulationSignalName, ignoreErrors: true);
        ScheduleSignal(UnemployedBeaversSignalName, ignoreErrors: true);
      }
    }

    void OnWorkerAssignmentChanged(object sender, WorkerChangedEventArgs args) {
      ScheduleSignal(UnemployedBeaversSignalName, ignoreErrors: true);
      ScheduleSignal(UnemployedBotsSignalName, ignoreErrors: true);
    }

    void FinishedBuildingRegisteredEvent(object sender, FinishedBuildingRegisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
      // Re-subscribe to pick up the new workplace's worker events.
      UnsubscribeFromWorkplaces();
      SubscribeToWorkplaces();
      ScheduleSignal(UnemployedBeaversSignalName, ignoreErrors: true);
      ScheduleSignal(UnemployedBotsSignalName, ignoreErrors: true);
    }

    void FinishedBuildingUnregisteredEvent(object sender, FinishedBuildingUnregisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
      // Re-subscribe to drop the destroyed workplace's worker events.
      UnsubscribeFromWorkplaces();
      SubscribeToWorkplaces();
      ScheduleSignal(UnemployedBeaversSignalName, ignoreErrors: true);
      ScheduleSignal(UnemployedBotsSignalName, ignoreErrors: true);
    }
  }

  #endregion
}
