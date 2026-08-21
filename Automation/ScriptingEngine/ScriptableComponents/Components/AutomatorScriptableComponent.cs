// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.Automation;
using Timberborn.EntitySystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class AutomatorScriptableComponent : ScriptableComponentBase {

  const string StateSignalLocKey = "IgorZ.Automation.Scriptable.Automator.Signal.State";
  const string RemoteStateSignalLocKey = "IgorZ.Automation.Scriptable.Automator.Signal.RemoteState";
  const string MissingRemoteStateSignalLocKey = "IgorZ.Automation.Scripting.Error.AutomatorSignalUnavailable";
  const string StateOnLocKey = "Automation.State.On";
  const string StateOffLocKey = "Automation.State.Off";

  const string StateSignalName = "Automator.State";
  const string RemoteStateSignalPrefix = "Automator.RemoteState.";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Automator";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    var result = new List<string>();
    var automator = behavior.GetComponent<Automator>();
    if (automator && automator.IsTransmitter) {
      result.Add(StateSignalName);
    }
    var ownerEntity = behavior.GetComponent<EntityComponent>();
    foreach (var transmitterId in _automatorRegistry.SortedTransmitterIds) {
      var entityId = Guid.Parse(transmitterId);
      if (ownerEntity != null && ownerEntity.EntityId == entityId) {
        continue;
      }
      result.Add(GetRemoteStateSignalName(entityId));
    }
    return result.ToArray();
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    if (name == StateSignalName) {
      var automator = GetComponentOrThrow<Automator>(behavior);
      return () => StateSignal(automator);
    }
    if (TryGetRemoteStateEntityId(name, out var entityId)) {
      return () => RemoteStateSignal(entityId);
    }
    throw new UnknownSignalException(name);
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    if (name == StateSignalName) {
      GetComponentOrThrow<Automator>(behavior);  // Verify only.
      return StateSignalDef;
    }
    if (TryGetRemoteStateEntityId(name, out var entityId)) {
      var automator = _automatorRegistry.FindTransmitterById(entityId);
      if (!automator) {
        throw new MissingRemoteStateSignalParsingError(entityId);
      }
      return MakeRemoteStateSignalDef(name, entityId, automator);
    }
    throw new UnknownSignalException(name);
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName == StateSignalName) {
      host.Behavior.GetOrCreate<AutomatorStateTracker>().AddSignal(signalOperator, host);
      return;
    }
    if (TryGetRemoteStateEntityId(signalOperator.SignalName, out var entityId)) {
      _signalReferenceManager.Register(entityId, signalOperator, host);
      return;
    }
    throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
  }
  
  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName == StateSignalName) {
      host.Behavior.GetOrThrow<AutomatorStateTracker>().RemoveSignal(signalOperator, host);
      return;
    }
    if (TryGetRemoteStateEntityId(signalOperator.SignalName, out var entityId)) {
      _signalReferenceManager.Unregister(entityId, signalOperator, host);
      return;
    }
    throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
  }

  #endregion

  #region Signals

  SignalDef StateSignalDef => _stateSignalDef ??= new SignalDef {
      ScriptName = StateSignalName,
      DisplayName = Loc.T(StateSignalLocKey),
      Scope = SignalDef.ScopeEnum.Building,
      Result = BooleanResultDef,
  };
  SignalDef _stateSignalDef;

  ValueDef BooleanResultDef => _booleanResultDef ??= new ValueDef {
      DisplayNumericFormat = ValueDef.NumericFormatEnum.Boolean,
      ValueType = ScriptValue.TypeEnum.Number,
      Options = [
          new DropdownItem {
              Value = ScriptValue.FromBool(true).AsRawNumber.ToString(),
              Text = Loc.T(StateOnLocKey),
          },
          new DropdownItem {
              Value = ScriptValue.FromBool(false).AsRawNumber.ToString(),
              Text = Loc.T(StateOffLocKey),
          },
      ],
  };
  ValueDef _booleanResultDef;

  SignalDef MakeRemoteStateSignalDef(string signalName, Guid entityId, Automator automator) {
    var displayNameArgument = automator.AutomatorName;
    return new SignalDef {
        ScriptName = signalName,
        DisplayNameFn = () => RemoteStateSignalDisplayName(entityId),
        DisplayNameLocKey = RemoteStateSignalLocKey,
        DisplayNameArgument = displayNameArgument,
        Scope = SignalDef.ScopeEnum.Global,
        Result = BooleanResultDef,
    };
  }

  static ScriptValue StateSignal(Automator automator) {
    return ScriptValue.FromInt(automator.State == AutomatorState.On ? 1 : 0);
  }

  ScriptValue RemoteStateSignal(Guid entityId) {
    var automator = _automatorRegistry.FindTransmitterById(entityId);
    if (!automator) {
      throw new MissingRemoteStateSignalError(entityId);
    }
    return StateSignal(automator);
  }

  #endregion

  #region Implementation

  readonly AutomatorRegistry _automatorRegistry;
  readonly AutomatorSignalReferenceManager _signalReferenceManager;

  AutomatorScriptableComponent(
      AutomatorRegistry automatorRegistry, AutomatorSignalReferenceManager signalReferenceManager) {
    _automatorRegistry = automatorRegistry;
    _signalReferenceManager = signalReferenceManager;
  }

  static string GetRemoteStateSignalName(Guid entityId) {
    return RemoteStateSignalPrefix + SignalNameSegment.Encode(entityId.ToString("N"));
  }

  static bool TryGetRemoteStateEntityId(string signalName, out Guid entityId) {
    entityId = Guid.Empty;
    if (!signalName.StartsWith(RemoteStateSignalPrefix, StringComparison.Ordinal)) {
      return false;
    }
    var encodedId = signalName[RemoteStateSignalPrefix.Length..];
    return SignalNameSegment.TryDecode(encodedId, out var rawId) && Guid.TryParseExact(rawId, "N", out entityId);
  }

  string RemoteStateSignalDisplayName(Guid entityId) {
    var automator = _automatorRegistry.FindTransmitterById(entityId);
    if (!automator) {
      throw new MissingRemoteStateSignalError(entityId);
    }
    return Loc.T(RemoteStateSignalLocKey, automator.AutomatorName);
  }

  sealed class MissingRemoteStateSignalError(Guid entityId)
      : ScriptError.RuntimeError(MissingRemoteStateSignalLocKey, $"Automator signal source is unavailable: {entityId}");

  sealed class MissingRemoteStateSignalParsingError(Guid entityId)
      : ScriptError.LocParsingError(
          MissingRemoteStateSignalLocKey, $"Automator signal source is unavailable: {entityId}");

  #endregion

  #region Tracker for the Automator state change

  internal sealed class AutomatorStateTracker : AbstractStatusTracker {
    public void OnStateChanged() {
      TriggerSignalUpdate(StateSignalName);
    }
  }

  #endregion
}
