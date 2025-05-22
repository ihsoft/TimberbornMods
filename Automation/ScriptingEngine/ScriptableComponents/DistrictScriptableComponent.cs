// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;
using Timberborn.Bots;
using Timberborn.DwellingSystem;
using Timberborn.GameDistricts;

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
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponentFast<DistrictBuilding>() 
        ? [BeaverPopulationSignalName, BotPopulationSignalName, NumberOfBedsSignalName]
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
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name is not (BeaverPopulationSignalName or BotPopulationSignalName or NumberOfBedsSignalName)) {
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
      if (!citizen || citizen.GetComponentFast<BotSpec>()) {
        ScheduleSignal(BotPopulationSignalName, ignoreErrors: true);
      }
      if (!citizen || !citizen.GetComponentFast<BotSpec>()) {
        ScheduleSignal(BeaverPopulationSignalName, ignoreErrors: true);
      }
    }

    void FinishedBuildingRegisteredEvent(object sender, FinishedBuildingRegisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
    }

    void FinishedBuildingUnregisteredEvent(object sender, FinishedBuildingUnregisteredEventArgs arg) {
      ScheduleSignal(NumberOfBedsSignalName, ignoreErrors: true);
    }
  }

  #endregion
}
