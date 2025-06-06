// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;
using IgorZ.Automation.Settings;
using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

class SignalDispatcher {

  #region API

  /// <summary>Registers a listener for signal changes.</summary>
  /// <remarks>
  /// The operator/listener pair must be unique. The for same listener, multiple operators cane be registered. No matter
  /// how many operators were registered for the listener, it gets only one notification when the signal changes.
  /// </remarks>
  /// <exception cref="InvalidOperationException">if operator/listener has already been registered.</exception>
  public void RegisterSignalListener(SignalOperator signalOperator, ISignalListener listener) {
    CheckIfChangesLocked();
    var (_, signalName) = ParseSignalName(signalOperator.SignalName);
    if (!_signalGroups.TryGetValue(signalName, out var group)) {
      DebugEx.Fine("Adding signal group: {0}", signalName);
      group = new SignalGroup();
      _signalGroups.Add(signalName, group);
    }
    if (!group.SignalSinks.TryGetValue(listener, out var consumers)) {
      consumers = [];
      group.SignalSinks.Add(listener, consumers);
    }
    if (!consumers.Add(signalOperator)) {
      throw new InvalidOperationException(
          $"Signal operator already registered for signal '{signalName}': {signalOperator}");
    }
  }

  /// <summary>Unregisters a listener for signal changes.</summary>
  /// <summary>The operator/listener pair must be registered first.</summary>
  /// <exception cref="InvalidOperationException">if operator/listener wasn't registered before.</exception>
  public void UnregisterSignalListener(SignalOperator signalOperator, ISignalListener listener) {
    CheckIfChangesLocked();
    var (_, signalName) = ParseSignalName(signalOperator.SignalName);
    if (!_signalGroups.TryGetValue(signalName, out var group)
        || !group.SignalSinks.TryGetValue(listener, out var consumers)
        || !consumers.Remove(signalOperator)) {
      throw new InvalidOperationException(
          $"Signal sink not registered for signal '{signalName}': {listener}/{signalOperator}");
    }
    if (consumers.Count == 0) {
      group.SignalSinks.Remove(listener);
    }
    if (group.SignalSinks.Count == 0 && group.Sources.Count == 0) {
      DebugEx.Fine("Removing signal group: name={0}, group={1}", signalName, group);
      _signalGroups.Remove(signalName);
    }
  }

  /// <summary>Gets the value of a signal.</summary>
  /// <remarks>Understands the signal name suffixes for aggregation: 'Min', 'Max', 'Sum', and 'Count'.</remarks>
  /// <returns>The requested value or '0' if no signal was created yet.</returns>
  public int GetSignalValue(string name) {
    var (signalType, signalName) = ParseSignalName(name);
    if (!_signalGroups.TryGetValue(signalName, out var group)) {
      return 0;  // Default value if the signal is not found.
    }
    if (group.IsDirty) {
      UpdateDirty(group);
    }
    return signalType switch {
      SignalType.Last => group.LastValue,
      SignalType.Count => group.CountValue,
      SignalType.Min => group.MinValue,
      SignalType.Max => group.MaxValue,
      SignalType.Sum => group.SumValue,
      SignalType.Avg => group.AvgValue,
      _ => throw new InvalidOperationException("Unknown signal type: " + signalType)
    };
  }

  /// <summary>Registers a signal provider for the given signal name.</summary>
  /// <remarks>The source/provider pair must be unique. This method triggers the signal update.</remarks>
  /// <exception cref="InvalidOperationException">if source/provider has been already registered.</exception>
  public void RegisterSignalProvider(string signalName, BaseComponent source, object provider) {
    CheckIfChangesLocked();
    if (!_signalGroups.TryGetValue(signalName, out var group)) {
      DebugEx.Fine("Adding signal group: {0}", signalName);
      group = new SignalGroup();
      _signalGroups.Add(signalName, group);
    }
    var entityId = source.GetComponentFast<EntityComponent>().EntityId.ToString();
    if (!group.Sources.TryGetValue(entityId, out var signalSource)) {
      signalSource = new SignalSource();
      group.Sources.Add(entityId, signalSource);
    }
    if (!signalSource.Providers.Add(provider)) {
      throw new InvalidOperationException("Provider already registered for signal: " + signalName);
    }
    UpdateSignalGroup(signalName, group);
  }

  void UpdateSignalGroup(string signalName, SignalGroup group) {
    group.IsDirty = true;
    if (!AutomationService.AutomationSystemReady) {
      return;  // Don't fire signals during the load stage.
    }
    using var _ = new LockChanges(
        this, $"Propagating signal group updates for '{signalName}'",
        description: "Don't register or unregister sinks or providers while handling the signals callbacks!");
    foreach (var entry in group.SignalSinks) {
      if (entry.Value.Count == 0) {
        throw new InvalidOperationException(
            "Signal sink has no registered operators: " + signalName + ", listener=" + entry.Key);
      }
      _scriptingService.ScheduleSignalCallback(
          new ScriptingService.SignalCallback(signalName, entry.Key), ignoreErrors: true);
    }
  }

  /// <summary>Unregisters a signal provider for the given signal name.</summary>
  /// <remarks>The source/provider pair must be registered. This method triggers the signal update.</remarks>
  /// <exception cref="InvalidOperationException">if source/provider is not registered.</exception>
  public void UnregisterSignalProvider(string signalName, BaseComponent source, object provider) {
    CheckIfChangesLocked();
    var entityId = source.GetComponentFast<EntityComponent>().EntityId.ToString();
    if (!_signalGroups.TryGetValue(signalName, out var group)
        || !group.Sources.TryGetValue(entityId, out var signalSource)
        || !signalSource.Providers.Remove(provider)) {
      throw new InvalidOperationException("Provider not registered for signal: " + signalName);
    }
    if (signalSource.Providers.Count == 0) {
      group.Sources.Remove(entityId);
    }
    if (group.Sources.Count == 0 && group.SignalSinks.Count == 0) {
      DebugEx.Fine("Removing signal group: name={0}, group={1}", signalName, group);
      _signalGroups.Remove(signalName);
    }
    UpdateSignalGroup(signalName, group);
  }

  /// <summary>Gets the names of all registered signals.</summary>
  /// <remarks>This includes registration for both the listeners and providers.</remarks>
  public List<string> GetRegisteredSignals() {
    return _signalGroups.Keys.ToList();
  }

  /// <summary>Sets the value of a signal.</summary>
  /// <remarks>
  /// The signal update will only be fired if the value has changed from the prvious value. The value is checked on a
  /// per-provider basis.
  /// </remarks>
  public void SetSignalValue(string signalName, int value, BaseComponent provider, bool ignoreErrors = false) {
    if (!_signalGroups.TryGetValue(signalName, out var group)) {
      throw new InvalidOperationException("Signal group not found for signal: " + signalName);
    }
    var entityId = provider.GetComponentFast<EntityComponent>().EntityId.ToString();
    if (!group.Sources.TryGetValue(entityId, out var source)) {
      throw new InvalidOperationException("Signal source not found for entity: " + entityId);
    }
    if (_automationDebugSettings.LogSignalsSetting.Value) {
      HostedDebugLog.Fine(provider, "Setting signal value: source={0}, value={1}", source, value);
    }
    if (source.Value == value && source.HasFirstValue) {
      return;
    }
    source.Value = value;
    source.HasFirstValue = true;
    group.LastValue = value;

    UpdateSignalGroup(signalName, group);
  }

  /// <summary>Serializes the state into a list of strings.</summary>
  public IEnumerable<string> ToPackedArray() {
    return _signalGroups.SelectMany(x => x.Value.Sources.Select(e => $"{x.Key}:{e.Key}={e.Value.Value}"));
  }

  /// <summary>Deserializes the state from a list of strings.</summary>
  /// <remarks>
  /// The state must be loaded in the normal game loading phase. Calling this method after the game initialization has
  /// complete will result in a wrong system behavior.
  /// </remarks>
  public void FromPackedArray(IEnumerable<string> packedValues) {
    _signalGroups.Clear();
    foreach (var packedValue in packedValues) {
      var parts = packedValue.Split(':');
      if (parts.Length != 2) {
        DebugEx.Error("Invalid packed signal value: {0}", packedValue);
        continue;
      }
      var signalName = parts[0];
      var entityIdAndValue = parts[1].Split('=');
      if (entityIdAndValue.Length != 2) {
        DebugEx.Warning("Invalid packed source value: {0}", packedValue);
        continue;
      }
      var entityId = entityIdAndValue[0];
      if (!int.TryParse(entityIdAndValue[1], out var value)) {
        DebugEx.Warning("Invalid signal value: {0}", entityIdAndValue[1]);
        continue;
      }
      DebugEx.Fine("[Automation system] Loading signal: {0}, entityId={1}, value={2}", signalName, entityId, value);
      var group = _signalGroups.GetOrAdd(signalName);
      var signalSource = group.Sources.GetOrAdd(entityId);
      signalSource.Value = value;
      signalSource.HasFirstValue = true;  // On load, all conditions will sync to the current value, default or not.
      group.IsDirty = true;  // Force recalculation of aggregates.
    }
  }

  #endregion

  #region Implementation

  record SignalSource {
    public int Value;
    public bool HasFirstValue;
    public readonly HashSet<object> Providers = [];

    public override string ToString() {
      return $"SignalSource(Value={Value}, HasFirstValue={HasFirstValue}, ProvidersCount={Providers.Count})";
    }
  }

  record SignalGroup {
    public int LastValue;
    public int MinValue;
    public int MaxValue;
    public int SumValue;
    public int AvgValue;
    public int CountValue;
    public bool IsDirty = true;
    public readonly Dictionary<string, SignalSource> Sources = [];
    public readonly Dictionary<ISignalListener, HashSet<object>> SignalSinks = [];

    public override string ToString() {
      return $"SignalGroup(LastValue={LastValue}, MinValue={MinValue}, MaxValue={MaxValue}, " +
          $"SumValue={SumValue}, CountValue={CountValue}, IsDirty={IsDirty}, " +
          $"SourcesCount={Sources.Count}, SinksCount={SignalSinks.Count})";
    }
  }

  /// <summary>Locks the system state for changes.</summary>
  /// <seealso cref="SignalDispatcher.CheckIfChangesLocked"/>
  class LockChanges : IDisposable {
    readonly SignalDispatcher _dispatcher;

    public LockChanges(SignalDispatcher dispatcher, string reason, string description = null) {
      _dispatcher = dispatcher;
      _dispatcher._systemStateLockReason = reason;
      _dispatcher._systemStateLockDescription = description;
    }

    public void Dispose() {
      _dispatcher._systemStateLockReason = null;
      _dispatcher._systemStateLockDescription = null;
    }
  }

  readonly Dictionary<string, SignalGroup> _signalGroups = [];
  readonly ScriptingService _scriptingService;
  readonly AutomationDebugSettings _automationDebugSettings;

  const string AggCountNameSuffix = ".Count";
  const string AggMinNameSuffix = ".Min";
  const string AggMaxNameSuffix = ".Max";
  const string AggSumNameSuffix = ".Sum";
  const string AggAvgNameSuffix = ".Avg";

  enum SignalType {
    Last, Count, Min, Max, Sum, Avg,
  }

  string _systemStateLockReason;
  string _systemStateLockDescription;

  [Inject]
  SignalDispatcher(
      ScriptingService scriptingService, AutomationDebugSettings automationDebugSettings, EventBus eventBus) {
    eventBus.Register(this);
    _scriptingService = scriptingService;
    _automationDebugSettings = automationDebugSettings;
  }

  static (SignalType signalType, string signalName) ParseSignalName(string name) {
    SignalType signalType;
    if (name.EndsWith(AggCountNameSuffix)) {
      signalType = SignalType.Count;
      name = name[..^AggCountNameSuffix.Length];
    } else if (name.EndsWith(AggMinNameSuffix)) {
      signalType = SignalType.Min;
      name = name[..^AggMinNameSuffix.Length];
    } else if (name.EndsWith(AggMaxNameSuffix)) {
      signalType = SignalType.Max;
      name = name[..^AggMaxNameSuffix.Length];
    } else if (name.EndsWith(AggSumNameSuffix)) {
      signalType = SignalType.Sum;
      name = name[..^AggSumNameSuffix.Length];
    } else if (name.EndsWith(AggAvgNameSuffix)) {
      signalType = SignalType.Avg;
      name = name[..^AggAvgNameSuffix.Length];
    } else {
      signalType = SignalType.Last;
    }
    return (signalType, name);
  }

  static void UpdateDirty(SignalGroup group) {
    var newMinValue = int.MaxValue;
    var newMaxValue = int.MinValue;
    var newSumValue = 0;
    foreach (var handler in group.Sources.Values) {
      newMinValue = Math.Min(newMinValue, handler.Value);
      newMaxValue = Math.Max(newMaxValue, handler.Value);
      newSumValue += handler.Value;
    }
    group.MinValue = newMinValue;
    group.MaxValue = newMaxValue;
    group.SumValue = newSumValue;
    group.AvgValue = group.CountValue > 0 ? newSumValue / group.CountValue : 0;
    group.CountValue = group.Sources.Count;
    group.IsDirty = false;
  }

  /// <summary>Checks if the system state is locked for changes.</summary>
  /// <remarks>
  /// Call it before dealing with the scripting system state. Changes to the system aren't allowed while serving API
  /// methods. All side effects that can change the system state must be delayed until the system is unlocked (returned
  /// from the API method).
  /// </remarks>
  void CheckIfChangesLocked() {
    if (_systemStateLockReason == null) {
      return;
    }
    if (_systemStateLockDescription != null) {
      DebugEx.Error("SignalDispatcher is locked for changes: {0}\n{1}",
                    _systemStateLockReason, _systemStateLockDescription);
    }
    throw new InvalidOperationException("SignalDispatcher is locked for changes: " + _systemStateLockReason);
  }

  [OnEvent]
  public void OnAutomationSystemReady(AutomationServiceReadyEvent serviceReadyEvent) {
    // Remove all groups that have no sources or sinks. It can happen if some buildings or rules failed to load.
    var unusedGroups = _signalGroups
        .Where(pair => pair.Value.Sources.Count == 0 && pair.Value.SignalSinks.Count == 0)
        .Select(pair => pair.Key)
        .ToList();
    foreach (var groupName in unusedGroups) {
      DebugEx.Warning("Removing unused signal group: " + groupName);
      _signalGroups.Remove(groupName);
    }
  }

  #endregion
}
