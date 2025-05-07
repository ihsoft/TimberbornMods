// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
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
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior _) {
    return () => ScriptValue.Of(
        !_signalHandlers.TryGetValue(name, out var signalHandler) ? -1 : signalHandler.Value);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior _) {
    if (!name.StartsWith(GetSignalSignalNamePrefix)) {
      throw new InvalidOperationException("Not a custom signal: " + name);
    }
    var signalName = name[GetSignalSignalNamePrefix.Length..];
    if (!SignalNameRegex.IsMatch(signalName)) {
      throw new ScriptError.ParsingError(
          $"Bad custom signal name: {signalName}. Must be alphanumeric and not start or end with a dot.");
    }
    return GetSignalDef with {
        ScriptName = name,
        DisplayName = Loc.T(GetSignalLocKey, signalName),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var signalDef = GetSignalHandler(signalOperator.SignalName, createIfNotFound: true);
    signalDef.References.AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var signalHandler = GetSignalHandler(signalOperator.SignalName);
    signalHandler.References.RemoveSignal(signalOperator, host);
    MaybeRemoveSignal(signalHandler);
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    return name switch {
        SetActionName => SetSignalAction,
        _ => base.GetActionExecutor(name, behavior),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        SetActionName => SetSignalActionDef,
        _ => base.GetActionDefinition(name, behavior),
    };
  }

  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName != SetActionName) {
      throw new InvalidOperationException("Unknown action: " + actionOperator.ActionName);
    }
    var shortSignalName = ((ConstantValueExpr)actionOperator.Operands[1]).ValueFn().AsString;
    var signalHandler = GetSignalHandler(GetSignalSignalNamePrefix + shortSignalName, createIfNotFound: true);
    signalHandler.References.AddAction(actionOperator);
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior _) {
    if (actionOperator.ActionName != SetActionName) {
      throw new InvalidOperationException("Unknown action: " + actionOperator.ActionName);
    }
    var shortSignalName = ((ConstantValueExpr)actionOperator.Operands[1]).ValueFn().AsString;
    var signalHandler = GetSignalHandler(GetSignalSignalNamePrefix + shortSignalName);
    signalHandler.References.RemoveAction(actionOperator);
    MaybeRemoveSignal(signalHandler);
  }

  #endregion

  #region Persistence implementation

  static readonly SingletonKey SignalsKey = new("IgorZ.Automation.SignalsScriptableComponent");
  static readonly ListKey<string> CustomSignalsKey = new("IgorZ.Automation.CustomSignals");

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    var objectSaver = singletonSaver.GetSingleton(SignalsKey);
    var packedValues = _signalHandlers.Select(entry => $"{entry.Key}:{entry.Value.Value}").ToList();
    objectSaver.Set(CustomSignalsKey, packedValues);
  }

  /// <inheritdoc/>
  public override void Load() {
    base.Load();
    if (!_singletonLoader.TryGetSingleton(SignalsKey, out var objectLoader)) {
      return;
    }
    var packedValues = objectLoader.Get(CustomSignalsKey);
    foreach (var packedValue in packedValues) {
      var pair = packedValue.Split(':');
      var signalHandler = new CustomSignalHandler(pair[0]) {
          Value = int.Parse(pair[1]),
          HasFirstValue = true,
      };
      _signalHandlers[signalHandler.Name] = signalHandler;
    }
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
              ValueValidator = SignalNameValidator,
          },
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
              NumberFormat = "0",
          },
      ],
  };
  ActionDef _setSignalActionDef;

  void SetSignalAction(ScriptValue[] args) {
    AssertActionArgsCount(SetActionName, args, 2);
    var signalName = GetSignalSignalNamePrefix + args[0].AsString;
    var value = args[1].AsNumber;
    
    if (!_signalHandlers.TryGetValue(signalName, out var signalHandler)) {
      throw new InvalidOperationException("Custom signal not registered: " + signalName);
    }
    if (signalHandler.Value == value && signalHandler.HasFirstValue) {
      return;
    }
    signalHandler.Value = value;
    signalHandler.HasFirstValue = true;
    signalHandler.References.ScheduleSignal(signalName, ScriptingService);
  }

  #endregion

  #region Implementation

  record CustomSignalHandler(string Name) {
    public int Value;
    public bool HasFirstValue;
    public readonly ReferenceManager References = new();
  }

  static readonly Regex SignalNameRegex = new(@"^(?!\.)([A-Za-z0-9]+\.?)*[A-Za-z0-9]$");

  readonly Dictionary<string, CustomSignalHandler> _signalHandlers = [];
  readonly ISingletonLoader _singletonLoader;

  SignalsScriptableComponent(ISingletonLoader singletonLoader) {
    _singletonLoader = singletonLoader;
  }

  void MaybeRemoveSignal(CustomSignalHandler signalHandler) {
    if (signalHandler.References.Signals.Count == 0 && signalHandler.References.Actions.Count == 0) {
      DebugEx.Fine("Removing custom signal: name={0}, value={1}", signalHandler.Name, signalHandler.Value);
      _signalHandlers.Remove(signalHandler.Name);
    }
  }

  CustomSignalHandler GetSignalHandler(string name, bool createIfNotFound = false) {
    if (!_signalHandlers.TryGetValue(name, out var signalHandler)) {
      if (createIfNotFound) {
        signalHandler = new CustomSignalHandler(name);
        _signalHandlers[name] = signalHandler;
        DebugEx.Fine("Creating custom signal: name={0}", name);
      } else {
        throw new InvalidOperationException("Custom signal not registered: " + name);
      }
    }
    return signalHandler;
  }

  static void SignalNameValidator(IValueExpr exp) {
    if (exp is not ConstantValueExpr constantValueExpr) {
      throw new ScriptError.ParsingError("Signal name must be a constant string: " + exp);
    }
    var name = constantValueExpr.ValueFn().AsString;
    if (!SignalNameRegex.IsMatch(name)) {
      throw new ScriptError.ParsingError(
          $"Bad custom signal name: {name}. Must be alphanumeric and not start or end with a dot.");
    }
  }

  #endregion
}
