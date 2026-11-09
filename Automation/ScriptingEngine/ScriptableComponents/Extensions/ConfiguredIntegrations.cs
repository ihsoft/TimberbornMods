// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bindito.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Newtonsoft.Json;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.Modding;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Discovers enabled mods' declarations, prepares early hooks and registers them during PostLoad.</summary>
sealed class ConfiguredIntegrations : IPostLoadableSingleton {
  const string ConfigFilePattern = "AutomationHooks*.json";
  static readonly EnumerationOptions ConfigSearchOptions = new() {
      MatchCasing = MatchCasing.CaseInsensitive, AttributesToSkip = 0, IgnoreInaccessible = false,
  };

  sealed record PendingTarget(
      HookConfiguration.Provider Provider, HookConfiguration.Target Definition,
      ConfiguredTarget Target, List<PendingSignal> Signals);
  sealed record PendingSignal(
      HookConfiguration.Signal Definition, HookMemberAccess.Reader Reader,
      Func<ValueDef> CreateValue, PropertySetterHooks.Observer Observer);

  /// <inheritdoc/>
  public void PostLoad() {
    foreach (var pending in _targets) {
      try {
        pending.Target.Bind(_container);
      } catch (Exception e) {
        foreach (var signal in pending.Signals) {
          _hooks.Remove(signal.Observer);
        }
        DebugEx.Error("[Automation Hooks] Cannot bind {0}: {1}", pending.Target.Type.FullName, e);
        continue;
      }
      foreach (var signal in pending.Signals) {
        try {
          RegisterSignal(pending, signal);
        } catch (Exception e) {
          _hooks.Remove(signal.Observer);
          LogFailure(pending.Provider.Id, signal.Definition.Id, e);
        }
      }
      foreach (var action in pending.Definition.Actions ?? []) {
        try {
          RegisterAction(pending, action);
        } catch (Exception e) {
          LogFailure(pending.Provider.Id, action?.Id, e);
        }
      }
    }
  }

  readonly IContainer _container;
  readonly ILoc _loc;
  readonly ExternalScriptableComponent _component;
  readonly PropertySetterHooks _hooks;
  readonly List<PendingTarget> _targets = [];

  ConfiguredIntegrations(IContainer container, ModRepository modRepository, ILoc loc,
      ExternalScriptableComponent component, PropertySetterHooks hooks) {
    _container = container;
    _loc = loc;
    _component = component;
    _hooks = hooks;
    // SingletonLifecycleService constructs lifecycle services before any Load calls. Patch before setter use.
    foreach (var mod in modRepository.EnabledMods) {
      LoadConfigurations(mod);
    }
  }

  void LoadConfigurations(Mod mod) {
    string[] paths;
    try {
      // Use the loader's selected version directory; Assembly.Location need not be a filesystem path.
      paths = Directory.EnumerateFiles(mod.ModDirectory.Path, ConfigFilePattern, ConfigSearchOptions)
          .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal).ToArray();
    } catch (Exception e) {
      DebugEx.Error("[Automation Hooks] Cannot scan {0}: {1}", mod.ModDirectory.Path, e);
      return;
    }
    foreach (var path in paths) {
      LoadConfiguration(path);
    }
  }

  void LoadConfiguration(string path) {
    try {
      var config = JsonConvert.DeserializeObject<HookConfiguration>(File.ReadAllText(path), new JsonSerializerSettings {
          MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 32,
      });
      if (config?.Version != 1 || config.Providers == null) {
        throw new InvalidDataException("Expected Version 1 and a Providers array.");
      }
      DebugEx.Info("[Automation Hooks] Loading {0}", path);
      foreach (var provider in config.Providers) {
        try {
          RequireName(provider?.Id, "Provider Id");
          foreach (var definition in provider.Targets ?? []) {
            PrepareTarget(provider, definition);
          }
        } catch (Exception e) {
          LogFailure(provider?.Id, null, e);
        }
      }
    } catch (Exception e) {
      DebugEx.Error("[Automation Hooks] Cannot load {0}: {1}", path, e);
    }
  }

  void PrepareTarget(HookConfiguration.Provider provider, HookConfiguration.Target definition) {
    try {
      var target = new ConfiguredTarget(definition);
      var signals = new List<PendingSignal>();
      foreach (var signal in definition.Signals ?? []) {
        PropertySetterHooks.Observer observer = null;
        try {
          RequireName(signal?.Id, "Signal Id");
          RequireName(signal.NameLocKey, "Signal NameLocKey");
          var name = MakeScriptName(provider.Id, signal.Id);
          var reader = HookMemberAccess.CompileReader(target.Type, target.IsStatic, signal.Read);
          var createValue = PrepareValue(signal.Value, reader.Type);
          var setters = (signal.Watch ?? [signal.Read.Split('.')[0]])
              .Select(name => HookMemberAccess.FindSetter(target, name)).ToArray();
          observer = new PropertySetterHooks.Observer { Name = name };
          _hooks.Add(setters, observer);
          signals.Add(new PendingSignal(signal, reader, createValue, observer));
        } catch (Exception e) {
          if (observer != null) {
            _hooks.Remove(observer);
          }
          LogFailure(provider.Id, signal?.Id, e);
        }
      }
      _targets.Add(new PendingTarget(provider, definition, target, signals));
    } catch (Exception e) {
      LogFailure(provider.Id, definition?.Type, e);
    }
  }

  void RegisterSignal(PendingTarget pending, PendingSignal signal) {
    var definition = signal.Definition;
    var target = pending.Target;
    var provider = pending.Provider;
    var value = signal.CreateValue();
    var native = new SignalDef {
        ScriptName = signal.Observer.Name, DisplayName = _loc.T(definition.NameLocKey), ProviderName = provider.Id,
        Scope = target.ResolveBuilding == null ? SignalDef.ScopeEnum.Global : SignalDef.ScopeEnum.Building,
        Result = value,
    };
    if (!_component.RegisterSignal(native, Read, target.ResolveBuilding)) {
      _hooks.Remove(signal.Observer);
      return;
    }
    signal.Observer.Notify = source => {
      if (target.Matches(source)) {
        _component.UpdateSignal(native.ScriptName, target.ResolveBuilding == null ? null : (BaseComponent)source);
      }
    };
    signal.Observer.Active = true;

    ScriptValue Read(BaseComponent source) {
      var result = HookValueConverter.ToScript(signal.Reader.Type, signal.Reader.Read(target.GetSource(source)));
      value.RuntimeValueValidator(result);
      return result;
    }
  }

  void RegisterAction(PendingTarget pending, HookConfiguration.Action action) {
    RequireName(action?.Id, "Action Id");
    RequireName(action.NameLocKey, "Action NameLocKey");
    var scriptName = MakeScriptName(pending.Provider.Id, action.Id);
    var arguments = action.Arguments ?? throw new ArgumentException("Action Arguments must be an array.");
    var types = arguments.Select(x => HookMemberAccess.ValueType(x.Type)).ToArray();
    var execute = HookMemberAccess.CompileAction(pending.Target, action.Method, types);
    var values = arguments.Select((argument, index) => PrepareValue(argument, types[index])()).ToArray();
    var native = new ActionDef {
        ScriptName = scriptName, DisplayName = _loc.T(action.NameLocKey), ProviderName = pending.Provider.Id,
        Arguments = values,
    };
    _component.RegisterAction(native, Execute, pending.Target.ResolveBuilding);

    void Execute(BaseComponent source, ScriptValue[] args) {
      var converted = new object[args.Length];
      for (var i = 0; i < args.Length; i++) {
        values[i].RuntimeValueValidator(args[i]);
        converted[i] = HookValueConverter.FromScript(types[i], args[i]);
      }
      execute(pending.Target.GetSource(source), converted);
    }
  }

  Func<ValueDef> PrepareValue(HookConfiguration.Value definition, Type type) {
    if (definition == null || definition.Type != null && HookMemberAccess.ValueType(definition.Type) != type) {
      throw new ArgumentException("Value metadata must match the getter or method parameter type.");
    }
    var options = definition.Options;
    HookValueFactory.Validate(type, definition, options);
    Func<HookConfiguration.Option[]> readOptions = null;
    if (definition.OptionsSource != null) {
      if (options != null || type != typeof(string)) {
        throw new ArgumentException("OptionsSource requires a string value without fixed Options.");
      }
      readOptions = PrepareOptions(definition.OptionsSource);
    }
    // Only registry contents and localization require the post-load phase. All metadata is checked above.
    return () => HookValueFactory.Create(type, definition, readOptions == null ? options : readOptions(), _loc);
  }

  Func<HookConfiguration.Option[]> PrepareOptions(HookConfiguration.OptionSource definition) {
    var target = new ConfiguredTarget(definition);
    if (target.ResolveBuilding != null) {
      throw new ArgumentException("OptionsSource must be Singleton or Static; options are shared editor metadata.");
    }
    var reader = HookMemberAccess.CompileReader(target.Type, target.IsStatic, definition.Read);
    var enumerableType = reader.Type.GetInterfaces().Prepend(reader.Type)
        .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    if (enumerableType == null) {
      throw new ArgumentException("OptionsSource Read must return an IEnumerable<T>.");
    }
    var itemType = enumerableType.GetGenericArguments()[0];
    var valueReader = HookMemberAccess.CompileReader(itemType, false, definition.ValuePath);
    var nameReader = HookMemberAccess.CompileReader(itemType, false, definition.NameLocKeyPath);
    if (valueReader.Type != typeof(string) || nameReader.Type != typeof(string)) {
      throw new ArgumentException("Option value and name paths must return strings.");
    }
    return () => {
      target.Bind(_container);
      var items = (IEnumerable)reader.Read(target.GetSource(null))
          ?? throw new ArgumentException("OptionsSource returned null.");
      var options = new List<HookConfiguration.Option>();
      foreach (var item in items) {
        var name = (string)nameReader.Read(item);
        // Registries may contain hidden/internal entries with no player-facing name.
        if (!string.IsNullOrWhiteSpace(name)) {
          options.Add(new HookConfiguration.Option { Value = (string)valueReader.Read(item), NameLocKey = name });
        }
      }
      return options.ToArray();
    };
  }

  /// <summary>Persistent format: Mods.providerId.capabilityId, with case-sensitive IDs preserved verbatim.</summary>
  /// <remarks>IDs must match [A-Za-z][A-Za-z0-9]*. Invalid IDs are rejected; no encoding is performed.</remarks>
  static string MakeScriptName(string providerId, string id) {
    if (!Regex.IsMatch(providerId ?? "", @"\A[A-Za-z][A-Za-z0-9]*\z")
        || !Regex.IsMatch(id ?? "", @"\A[A-Za-z][A-Za-z0-9]*\z")) {
      throw new ArgumentException("Provider and capability IDs must match [A-Za-z][A-Za-z0-9]*.");
    }
    return "Mods." + providerId + "." + id;
  }

  static void RequireName(string value, string name) {
    if (string.IsNullOrWhiteSpace(value)) {
      throw new ArgumentException(name + " is required.");
    }
  }

  static void LogFailure(string provider, string id, Exception error) {
    if (error is ConfiguredTarget.AssemblyNotLoadedException missingAssembly) {
      DebugEx.Info("[Automation Hooks] Skipping {0}.{1}: assembly '{2}' is not loaded.",
          provider, id, missingAssembly.AssemblyName);
      return;
    }
    DebugEx.Error("[Automation Hooks] Cannot register {0}.{1}: {2}", provider, id, error);
  }
}
