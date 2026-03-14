// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.PrioritySystem;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class WorkplaceScriptableComponent : ScriptableComponentBase {

  const string RemoveWorkersActionLocKey = "IgorZ.Automation.Scriptable.Workplace.Action.RemoveWorkers";
  const string SetWorkersActionLocKey = "IgorZ.Automation.Scriptable.Workplace.Action.SetWorkers";
  const string SetPriorityActionLocKey = "IgorZ.Automation.Scriptable.Workplace.Action.SetPriority";
  const string AssignedWorkersSignalLocKey = "IgorZ.Automation.Scriptable.Workplace.Signal.AssignedWorkers";

  const string RemoveWorkersActionName = "Workplace.RemoveWorkers";
  const string SetWorkersActionName = "Workplace.SetWorkers";
  const string SetPriorityActionName = "Workplace.SetPriority";
  const string AssignedWorkersSignalName = "Workplace.AssignedWorkers";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Workplace";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior, throwIfNotFound: false);
    return workplace ? [AssignedWorkersSignalName] : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    return name switch {
        AssignedWorkersSignalName => () => ScriptValue.FromInt(workplace.NumberOfAssignedWorkers),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    return name switch {
        AssignedWorkersSignalName =>
            _signalDefsCache.GetOrAdd(name, workplace.MaxWorkers, MakeAssignedWorkersSignalDef),
        _ => throw new UnknownSignalException(name),
    };
  }
  readonly ObjectsCache<SignalDef> _signalDefsCache = new();

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name == AssignedWorkersSignalName) {
      host.Behavior.GetOrCreate<WorkplaceChangeTracker>().AddSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name== AssignedWorkersSignalName) {
      host.Behavior.GetOrThrow<WorkplaceChangeTracker>().RemoveSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior, throwIfNotFound: false);
    if (!workplace) {
      return [];
    }
    var workplacePriority = behavior.GetComponent<WorkplacePriority>();
    return workplacePriority
        ? [RemoveWorkersActionName, SetWorkersActionName, SetPriorityActionName]
        : [RemoveWorkersActionName, SetWorkersActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    if (name == SetPriorityActionName) {
      var priorityComponent = behavior.GetComponent<WorkplacePriority>();
      if (!priorityComponent) {
        throw new ScriptError.BadStateError(behavior, "Building doesn't have WorkplacePriority");
      }
      return args => SetPriorityAction(priorityComponent, args);
    }
    return name switch {
        RemoveWorkersActionName => _ => ResetWorkersAction(workplace),
        SetWorkersActionName => args => SetWorkersAction(workplace, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    return name switch {
        RemoveWorkersActionName => RemoveWorkersActionDef,
        SetPriorityActionName => SetPriorityActionDef,
        SetWorkersActionName => _actionDefsCache.GetOrAdd(name, workplace.MaxWorkers, MakeSetWorkersActionDef),
        _ => throw new UnknownActionException(name),
    };
  }
  readonly ObjectsCache<ActionDef> _actionDefsCache = new();

  #endregion

  #region Signals

  SignalDef MakeAssignedWorkersSignalDef(string _, int maxWorkers) {
    return new SignalDef {
        ScriptName = AssignedWorkersSignalName,
        DisplayName = Loc.T(AssignedWorkersSignalLocKey),
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
            DisplayNumericFormatRange = (0, maxWorkers),
        },
    };
  }

  #endregion

  #region Actions

  ActionDef RemoveWorkersActionDef => _removeWorkersActionDef ??= new ActionDef {
      ScriptName = RemoveWorkersActionName,
      DisplayName = Loc.T(RemoveWorkersActionLocKey),
      Arguments = [],
  };
  ActionDef _removeWorkersActionDef;

  ActionDef MakeSetWorkersActionDef(string _, int maxWorkers) {
    return new ActionDef {
        ScriptName = SetWorkersActionName,
        DisplayName = Loc.T(SetWorkersActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.Number,
                DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
                DisplayNumericFormatRange = (0, maxWorkers),
                RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0, maxWorkers),
            },
        ],
    };
  }

  ActionDef SetPriorityActionDef => _setPriorityActionDef ??= new ActionDef {
      ScriptName = SetPriorityActionName,
      DisplayName = Loc.T(SetPriorityActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
              Options = [
                  ("VeryLow", Loc.T("Priorities.VeryLow")),
                  ("Low", Loc.T("Priorities.Low")),
                  ("Normal", Loc.T("Priorities.Normal")),
                  ("High", Loc.T("Priorities.High")),
                  ("VeryHigh", Loc.T("Priorities.VeryHigh")),
              ],
          },
      ],
  };
  ActionDef _setPriorityActionDef;

  static void ResetWorkersAction(Workplace building) {
    SetWorkersAction(building, [ScriptValue.FromInt(0)]);
  }

  static void SetWorkersAction(Workplace building, ScriptValue[] args) {
    AssertActionArgsCount(SetWorkersActionName, args, 1);
    var numWorkers = args[0].AsInt;
    if (numWorkers < 0 || numWorkers > building.MaxWorkers) {
      throw new ScriptError.ValueOutOfRange($"Number of workers out of range: {numWorkers}");
    }
    if (building.DesiredWorkers == numWorkers) {
      return;
    }
    building.DesiredWorkers = numWorkers;
    building.UnassignWorkerIfOverstaffed();
  }

  static void SetPriorityAction(WorkplacePriority workplacePriority, ScriptValue[] args) {
    AssertActionArgsCount(SetPriorityActionName, args, 1);
    var priorityName = args[0].AsString;
    if (!Enum.TryParse<Priority>(priorityName, out var priority)) {
      throw new ScriptError.ValueOutOfRange($"Unknown priority: {priorityName}");
    }
    if (workplacePriority.Priority == priority) {
      return;
    }
    workplacePriority.SetPriority(priority);
  }

  #endregion

  #region Implementation

  static Workplace GetWorkplace(BaseComponent building, bool throwIfNotFound = true) {
    var workplace = building.GetComponent<Workplace>();
    if (!workplace && throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Building doesn't have Workplace");
    }
    return workplace;
  }

  #endregion

  #region Workplace change tracker

  internal sealed class WorkplaceChangeTracker : AbstractStatusTracker {

    /// <inheritdoc/>
    public override void Start() {
      base.Start();
      var workplace = AutomationBehavior.GetComponent<Workplace>();
      workplace.WorkerAssigned += OnWorkerChanged;
      workplace.WorkerUnassigned += OnWorkerChanged;
    }

    void OnWorkerChanged(object sender, WorkerChangedEventArgs args) {
      ScheduleSignal(AssignedWorkersSignalName, ignoreErrors: true);
    }
  }

  #endregion
}
