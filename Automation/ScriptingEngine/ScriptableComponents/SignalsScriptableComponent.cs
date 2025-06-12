// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.Settings;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

class SignalsScriptableComponent : ScriptableComponentBase, ISaveableSingleton {

  const string GetSignalLocKey = "IgorZ.Automation.Scriptable.Signals.Signal.Get";
  const string SetSignalActionLocKey = "IgorZ.Automation.Scriptable.Signals.Action.Set";

  const string GetSignalSignalNamePrefix = "Signals.";
  const string SetActionName = "Signals.Set";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Signals";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior _) {
    return _signalDispatcher.GetRegisteredSignals().OrderBy(x => x).ToArray();
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior _) {
    return () => ScriptValue.Of(_signalDispatcher.GetSignalValue(name));
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior _) {
    if (!name.StartsWith(GetSignalSignalNamePrefix)) {
      throw new InvalidOperationException("Not a custom signal: " + name);
    }
    var signalName = name[GetSignalSignalNamePrefix.Length..];
    SymbolExpr.CheckName(signalName);
    return GetSignalDef with {
        ScriptName = name,
        DisplayName = Loc.T(GetSignalLocKey, signalName),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _signalDispatcher.RegisterSignalListener(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _signalDispatcher.UnregisterSignalListener(signalOperator, host);
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return [SetActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return name switch {
        SetActionName => args => SetSignalAction(args, behavior),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        SetActionName => SetSignalActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName == SetActionName) {
      var signalName = GetSignalSignalNamePrefix + ((ConstantValueExpr)actionOperator.Operands[1]).ValueFn().AsString;
      _signalDispatcher.RegisterSignalProvider(signalName, behavior, actionOperator);
    }
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName == SetActionName) {
      var signalName = GetSignalSignalNamePrefix + ((ConstantValueExpr)actionOperator.Operands[1]).ValueFn().AsString;
      _signalDispatcher.UnregisterSignalProvider(signalName, behavior, actionOperator);
    }
  }

  #endregion

  #region Persistence implementation

  static readonly SingletonKey SignalsKey = new("IgorZ.Automation.SignalsScriptableComponent");
  static readonly ListKey<string> CustomSignalsKey = new("IgorZ.Automation.CustomSignals.Proto");

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    var objectSaver = singletonSaver.GetSingleton(SignalsKey);
    objectSaver.Set(CustomSignalsKey, _signalDispatcher.ToPackedArray().ToList());
  }

  /// <inheritdoc/>
  public override void Load() {
    base.Load();
    if (!_singletonLoader.TryGetSingleton(SignalsKey, out var objectLoader)) {
      return;
    }
    // FIXME: Compatibility with old saves prior to v2.5.1. Drop it in the future.
    if (!objectLoader.Has(CustomSignalsKey)) {
      DebugEx.Warning("Skipping old signals state loading! All signals are reset to value 0.");
      return;
    }
    var packedSignals = objectLoader.Get(CustomSignalsKey);
    if (AutomationDebugSettings.ResetSignalsOnLoad) {
      DebugEx.Warning("Not restoring {0} signals from the save file.", packedSignals.Count);
      return;
    }
    _signalDispatcher.FromPackedArray(packedSignals);
  }

  #endregion

  #region Signals

  SignalDef GetSignalDef => _seasonSignalDef ??= new SignalDef {
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
      },
  };
  SignalDef _seasonSignalDef;

  #endregion

  #region Actions

  ActionDef SetSignalActionDef => _setSignalActionDef ??= new ActionDef {
      ScriptName = SetActionName,
      DisplayName = Loc.T(SetSignalActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.String,
              ArgumentValidator = SignalNameValidator,
          },
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
          },
      ],
  };
  ActionDef _setSignalActionDef;

  void SetSignalAction(ScriptValue[] args, AutomationBehavior behavior) {
    AssertActionArgsCount(SetActionName, args, 2);
    var signalName = GetSignalSignalNamePrefix + args[0].AsString;
    _signalDispatcher.SetSignalValue(signalName, args[1].AsNumber, behavior);
  }

  #endregion

  #region Implementation

  readonly ISingletonLoader _singletonLoader;
  readonly SignalDispatcher _signalDispatcher;

  SignalsScriptableComponent(ISingletonLoader singletonLoader, SignalDispatcher signalDispatcher) {
    _singletonLoader = singletonLoader;
    _signalDispatcher = signalDispatcher;
  }

  static void SignalNameValidator(IValueExpr exp) {
    if (exp is not ConstantValueExpr constantValueExpr) {
      throw new ScriptError.ParsingError("Signal name must be a constant string: " + exp);
    }
    var name = constantValueExpr.ValueFn().AsString;
    SymbolExpr.CheckName(name);
  }

  #endregion
}
