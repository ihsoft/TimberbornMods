// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.Bots;
using Timberborn.GameDistricts;
using Timberborn.Navigation;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

class DistrictScriptableComponent : ScriptableComponentBase {

  const string BotPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Bots";
  const string BeaversPopulationSignalLocKey = "IgorZ.Automation.Scriptable.District.Signal.Beavers";

  const string BotPopulationSignalName = "District.Bots";
  const string BeaverPopulationSignalName = "District.Beavers";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "District";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      return [];
    }
    return [BeaverPopulationSignalName, BotPopulationSignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError("Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => () => ScriptValue.FromInt(GetPopulation(districtBuilding).Beavers.Count),
        BotPopulationSignalName => () => ScriptValue.FromInt(GetPopulation(districtBuilding).NumberOfBots),
        _ => throw new ScriptError("Unknown signal: " + name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var districtBuilding = building.GetComponentFast<DistrictBuilding>();
    if (!districtBuilding) {
      throw new ScriptError("Not a district building");
    }
    return name switch {
        BeaverPopulationSignalName => BeaverPopulationSignalDef,
        BotPopulationSignalName => BotPopulationSignalDef,
        _ => throw new ScriptError("Unknown signal: " + name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    var tracker = building.GetComponentFast<DistrictChangeTracker>()
        ?? _instantiator.AddComponent<DistrictChangeTracker>(building.GameObjectFast);
    switch (name) {
      case BeaverPopulationSignalName:
        tracker.OnBeaverPopulationChanged.Add(onValueChanged);
        break;
      case BotPopulationSignalName:
        tracker.OnBotPopulationChanged.Add(onValueChanged);
        break;
      default:
        throw new ScriptError("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged) {
    var tracker = building.GetComponentFast<DistrictChangeTracker>();
    if (tracker) {
      tracker.OnBeaverPopulationChanged.Remove(onValueChanged);
      tracker.OnBotPopulationChanged.Remove(onValueChanged);
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

  #endregion

  #region Implementation

  readonly BaseInstantiator _instantiator;

  DistrictScriptableComponent(BaseInstantiator instantiator) {
    _instantiator = instantiator;
  }

  static DistrictPopulation GetPopulation(DistrictBuilding districtBuilding, bool throwIfNotAttached = true) {
    var district = districtBuilding.District;
    if (!district && throwIfNotAttached) {
      throw new ExecutionInterrupted("Not attached to a district");
    }
    return district?.DistrictPopulation;
  }

  #endregion

  #region District citizens tracker

  sealed class DistrictChangeTracker : BaseComponent {

    public readonly List<Action> OnBotPopulationChanged = [];
    public readonly List<Action> OnBeaverPopulationChanged = [];
    
    DistrictCenter _currentDistrictCenter;

    void Awake() {
      var districtBuilding = GetComponentFast<DistrictBuilding>();
      districtBuilding.ReassignedDistrict += OnDistrictChangedEvent;
      districtBuilding.ReassignedConstructionDistrict += OnDistrictChangedEvent;
      UpdateDistrictCenter();
    }

    void UpdateDistrictCenter() {
      if (_currentDistrictCenter) {
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned -= OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned -= OnCitizenUnassigned;
      }
      _currentDistrictCenter = GetComponentFast<DistrictBuilding>().District;
      if (_currentDistrictCenter) {
        _currentDistrictCenter.DistrictPopulation.CitizenAssigned += OnCitizenAssigned;
        _currentDistrictCenter.DistrictPopulation.CitizenUnassigned += OnCitizenUnassigned;
      }
    }

    void OnDistrictChangedEvent(object obj, EventArgs args) {
      foreach (var action in OnBotPopulationChanged) {
        action();
      }
      foreach (var action in OnBeaverPopulationChanged) {
        action();
      }
    }

    void OnCitizenAssigned(object sender, CitizenAssignedEventArgs args) {
      OnPopulationChangedEvent(args.Citizen);
    }

    void OnCitizenUnassigned(object sender, CitizenUnassignedEventArgs args) {
      OnPopulationChangedEvent(args.Citizen);
    }

    void OnPopulationChangedEvent(Citizen citizen) {
      if (citizen.GetComponentFast<BotSpec>()) {
        foreach (var action in OnBotPopulationChanged) {
          action();
        }
      } else {
        foreach (var action in OnBeaverPopulationChanged) {
          action();
        }
      }
    }
  }

  #endregion
}
