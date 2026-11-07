// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Bindito.Core;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.Api;

/// <summary>Copy this file into a mod to expose capabilities without referencing AdvancedAutomation.dll.</summary>
/// <remarks>
/// Create one instance per game context and declare capabilities in IPostLoadableSingleton.PostLoad.
/// IDs are persistent; names and option labels are localization keys. Types: bool, int, float, double, string.
/// Provider and capability IDs must match [A-Za-z][A-Za-z0-9]*; invalid IDs are rejected, never encoded.
/// For duplicate signals or actions with the same provider and ID, Automation logs an error and keeps the first.
/// Optional min/max restrict editor input only; they do not validate signal reads or action execution.
/// Numbers use two-decimal precision. All calls run on the game thread. No scripting or UI dependencies are required.
/// </remarks>
sealed class AutomationApi {
  const string EndpointTypeName =
      "IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions.AutomationApiEndpoint";
  const string EndpointMethodName = "ConnectV1";

  /// <summary>Creates a provider. Automation may be absent; connection is resolved lazily.</summary>
  public AutomationApi(IContainer container, string modId, string modNameLocKey) {
    _container = container ?? throw new ArgumentNullException(nameof(container));
    _modId = RequiredId(modId, nameof(modId));
    _modNameLocKey = Required(modNameLocKey, nameof(modNameLocKey));
  }

  /// <summary>Declares a global bool signal.</summary>
  public void AddSignal(string id, string nameLocKey, Func<bool> read) {
    DeclareSignal(id, nameLocKey, read);
  }

  /// <summary>Declares a global int signal.</summary>
  public void AddSignal(string id, string nameLocKey, Func<int> read, int? min = null, int? max = null) {
    DeclareSignal(id, nameLocKey, read, min: min, max: max);
  }

  /// <summary>Declares a global float signal.</summary>
  public void AddSignal(string id, string nameLocKey, Func<float> read, float? min = null, float? max = null) {
    DeclareSignal(id, nameLocKey, read, min: min, max: max);
  }

  /// <summary>Declares a global double signal.</summary>
  public void AddSignal(string id, string nameLocKey, Func<double> read, double? min = null, double? max = null) {
    DeclareSignal(id, nameLocKey, read, min: min, max: max);
  }

  /// <summary>Declares a global string signal.</summary>
  public void AddSignal(string id, string nameLocKey, Func<string> read,
      (string Value, string NameLocKey)[] options = null) {
    DeclareSignal(id, nameLocKey, read, options: options);
  }

  /// <summary>Declares a percentage signal; values are fractions (0.75 means 75%).</summary>
  public void AddPercentSignal(string id, string nameLocKey, Func<float> read, float? min = null, float? max = null) {
    DeclareSignal(id, nameLocKey, read, min: min, max: max, percent: true);
  }

  /// <summary>Declares a percentage signal; values are fractions (0.75 means 75%).</summary>
  public void AddPercentSignal(string id, string nameLocKey, Func<double> read,
      double? min = null, double? max = null) {
    DeclareSignal(id, nameLocKey, read, min: min, max: max, percent: true);
  }

  /// <summary>Declares a global action without arguments.</summary>
  public void AddAction(string id, string nameLocKey, Action execute) {
    if (execute == null) {
      throw new ArgumentNullException(nameof(execute));
    }
    AddAction(id, nameLocKey, null, (_, _) => execute(), null, null, null, null);
  }

  /// <summary>Declares a global bool action.</summary>
  public void AddAction(string id, string nameLocKey, Action<bool> execute) {
    DeclareAction(id, nameLocKey, execute);
  }

  /// <summary>Declares a global int action.</summary>
  public void AddAction(string id, string nameLocKey, Action<int> execute, int? min = null, int? max = null) {
    DeclareAction(id, nameLocKey, execute, min: min, max: max);
  }

  /// <summary>Declares a global float action.</summary>
  public void AddAction(string id, string nameLocKey, Action<float> execute, float? min = null, float? max = null) {
    DeclareAction(id, nameLocKey, execute, min: min, max: max);
  }

  /// <summary>Declares a global double action.</summary>
  public void AddAction(string id, string nameLocKey, Action<double> execute, double? min = null, double? max = null) {
    DeclareAction(id, nameLocKey, execute, min: min, max: max);
  }

  /// <summary>Declares a global string action.</summary>
  public void AddAction(string id, string nameLocKey, Action<string> execute,
      (string Value, string NameLocKey)[] options = null) {
    DeclareAction(id, nameLocKey, execute, options: options);
  }

  /// <summary>Declares a percentage action; values and bounds are fractions (0.75 means 75%).</summary>
  public void AddPercentAction(string id, string nameLocKey, Action<float> execute,
      float? min = null, float? max = null) {
    DeclareAction(id, nameLocKey, execute, min: min, max: max, percent: true);
  }

  /// <summary>Declares a percentage action; values and bounds are fractions (0.75 means 75%).</summary>
  public void AddPercentAction(string id, string nameLocKey, Action<double> execute,
      double? min = null, double? max = null) {
    DeclareAction(id, nameLocKey, execute, min: min, max: max, percent: true);
  }

  /// <summary>Exposes capabilities on buildings with this component. IDs remain unique within the provider.</summary>
  public BuildingApi<T> ForBuildings<T>() where T : BaseComponent => new(this);

  /// <summary>Queues a possible change. Omit source for a global signal; otherwise pass a building component.</summary>
  /// <remarks>
  /// Never calls a getter or executes rules synchronously. Processing happens later on the game thread and reads the
  /// latest value. Repeated updates may coalesce; intermediate states are not guaranteed. Notifications raised by
  /// actions wait for a later pass. Without Automation or listeners no evaluation occurs. This is not an event stream.
  /// </remarks>
  public void UpdateSignal(string id, BaseComponent source = null) {
    if (Connect()) {
      PublishDeclarations();
      _update(_modId, id, source);
    }
  }

  /// <summary>Typed building helpers. The mod's component type never crosses the assembly boundary.</summary>
  public sealed class BuildingApi<T> where T : BaseComponent {
    readonly AutomationApi _api;

    internal BuildingApi(AutomationApi api) {
      _api = api;
    }

    /// <summary>Declares a building bool signal.</summary>
    public void AddSignal(string id, string nameLocKey, Func<T, bool> read) {
      DeclareSignal(id, nameLocKey, read);
    }

    /// <summary>Declares a building int signal.</summary>
    public void AddSignal(string id, string nameLocKey, Func<T, int> read, int? min = null, int? max = null) {
      DeclareSignal(id, nameLocKey, read, min: min, max: max);
    }

    /// <summary>Declares a building float signal.</summary>
    public void AddSignal(string id, string nameLocKey, Func<T, float> read, float? min = null, float? max = null) {
      DeclareSignal(id, nameLocKey, read, min: min, max: max);
    }

    /// <summary>Declares a building double signal.</summary>
    public void AddSignal(string id, string nameLocKey, Func<T, double> read, double? min = null, double? max = null) {
      DeclareSignal(id, nameLocKey, read, min: min, max: max);
    }

    /// <summary>Declares a building string signal.</summary>
    public void AddSignal(string id, string nameLocKey, Func<T, string> read,
        (string Value, string NameLocKey)[] options = null) {
      DeclareSignal(id, nameLocKey, read, options: options);
    }

    /// <summary>Declares a percentage signal; values are fractions (0.75 means 75%).</summary>
    public void AddPercentSignal(string id, string nameLocKey, Func<T, float> read,
        float? min = null, float? max = null) {
      DeclareSignal(id, nameLocKey, read, min: min, max: max, percent: true);
    }

    /// <summary>Declares a percentage signal; values are fractions (0.75 means 75%).</summary>
    public void AddPercentSignal(string id, string nameLocKey, Func<T, double> read,
        double? min = null, double? max = null) {
      DeclareSignal(id, nameLocKey, read, min: min, max: max, percent: true);
    }

    /// <summary>Declares an action without arguments on each matching building.</summary>
    public void AddAction(string id, string nameLocKey, Action<T> execute) {
      if (execute == null) {
        throw new ArgumentNullException(nameof(execute));
      }
      _api.AddAction(id, nameLocKey, null, (source, _) => execute((T)source), Resolve, null, null, null);
    }

    /// <summary>Declares a building bool action.</summary>
    public void AddAction(string id, string nameLocKey, Action<T, bool> execute) {
      DeclareAction(id, nameLocKey, execute);
    }

    /// <summary>Declares a building int action.</summary>
    public void AddAction(string id, string nameLocKey, Action<T, int> execute, int? min = null, int? max = null) {
      DeclareAction(id, nameLocKey, execute, min: min, max: max);
    }

    /// <summary>Declares a building float action.</summary>
    public void AddAction(string id, string nameLocKey, Action<T, float> execute,
        float? min = null, float? max = null) {
      DeclareAction(id, nameLocKey, execute, min: min, max: max);
    }

    /// <summary>Declares a building double action.</summary>
    public void AddAction(string id, string nameLocKey, Action<T, double> execute,
        double? min = null, double? max = null) {
      DeclareAction(id, nameLocKey, execute, min: min, max: max);
    }

    /// <summary>Declares a building string action.</summary>
    public void AddAction(string id, string nameLocKey, Action<T, string> execute,
        (string Value, string NameLocKey)[] options = null) {
      DeclareAction(id, nameLocKey, execute, options: options);
    }

    /// <summary>Declares a percentage action; values and bounds are fractions (0.75 means 75%).</summary>
    public void AddPercentAction(string id, string nameLocKey, Action<T, float> execute,
        float? min = null, float? max = null) {
      DeclareAction(id, nameLocKey, execute, min: min, max: max, percent: true);
    }

    /// <summary>Declares a percentage action; values and bounds are fractions (0.75 means 75%).</summary>
    public void AddPercentAction(string id, string nameLocKey, Action<T, double> execute,
        double? min = null, double? max = null) {
      DeclareAction(id, nameLocKey, execute, min: min, max: max, percent: true);
    }

    void DeclareSignal<TValue>(string id, string nameLocKey, Func<T, TValue> read, double? min = null,
        double? max = null, (string Value, string NameLocKey)[] options = null, bool percent = false) {
      if (read == null) {
        throw new ArgumentNullException(nameof(read));
      }
      _api.AddSignal(id, nameLocKey, typeof(TValue), source => read((T)source), Resolve, options, min, max, percent);
    }

    void DeclareAction<TValue>(string id, string nameLocKey, Action<T, TValue> execute, double? min = null,
        double? max = null, (string Value, string NameLocKey)[] options = null, bool percent = false) {
      if (execute == null) {
        throw new ArgumentNullException(nameof(execute));
      }
      _api.AddAction(id, nameLocKey, typeof(TValue), (source, value) => execute((T)source, (TValue)value),
          Resolve, min, max, options, percent);
    }

    static BaseComponent Resolve(BaseComponent source) => source ? source.GetComponent<T>() : null;
  }

  readonly IContainer _container;
  readonly string _modId;
  readonly string _modNameLocKey;
  readonly List<IDictionary<string, object>> _declarations = new();
  Func<IContainer, IDictionary<string, Delegate>> _connect;
  Action<IDictionary<string, object>> _register;
  Action<string, string, BaseComponent> _update;
  Func<bool> _isActive;
  bool _searched;
  int _published;

  void DeclareSignal<TValue>(string id, string nameLocKey, Func<TValue> read, double? min = null, double? max = null,
      (string Value, string NameLocKey)[] options = null, bool percent = false) {
    if (read == null) {
      throw new ArgumentNullException(nameof(read));
    }
    AddSignal(id, nameLocKey, typeof(TValue), _ => read(), null, options, min, max, percent);
  }

  void DeclareAction<TValue>(string id, string nameLocKey, Action<TValue> execute, double? min = null,
      double? max = null, (string Value, string NameLocKey)[] options = null, bool percent = false) {
    if (execute == null) {
      throw new ArgumentNullException(nameof(execute));
    }
    AddAction(id, nameLocKey, typeof(TValue), (_, value) => execute((TValue)value), null, min, max, options, percent);
  }

  void AddSignal(string id, string nameLocKey, Type type, Func<BaseComponent, object> read,
      Func<BaseComponent, BaseComponent> resolve, (string Value, string NameLocKey)[] options, double? min = null,
      double? max = null, bool percent = false) {
    var definition = MakeDefinition("signal", id, nameLocKey, type, resolve, options, min, max, percent);
    definition["read"] = read;
    AddDeclaration(definition);
  }

  void AddAction(string id, string nameLocKey, Type type, Action<BaseComponent, object> execute,
      Func<BaseComponent, BaseComponent> resolve, double? min, double? max, (string Value, string NameLocKey)[] options,
      bool percent = false) {
    var definition = MakeDefinition("action", id, nameLocKey, type, resolve, options, min, max, percent);
    definition["execute"] = execute;
    AddDeclaration(definition);
  }

  IDictionary<string, object> MakeDefinition(string kind, string id, string nameLocKey, Type type,
      Func<BaseComponent, BaseComponent> resolve, (string Value, string NameLocKey)[] options, double? min, double? max,
      bool percent) {
    if (type != null && type != typeof(bool) && type != typeof(int)
        && type != typeof(float) && type != typeof(double) && type != typeof(string)) {
      throw new ArgumentException("Supported value types: bool, int, float, double, string.");
    }
    if ((min.HasValue || max.HasValue) && type != typeof(int) && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Bounds require an int, float or double value.");
    }
    if (min.HasValue && (double.IsNaN(min.Value) || double.IsInfinity(min.Value))
        || max.HasValue && (double.IsNaN(max.Value) || double.IsInfinity(max.Value)) || min > max) {
      throw new ArgumentException("Invalid numeric bounds.");
    }
    if (percent && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Percentages require a float or double value.");
    }
    if (options != null && (type != typeof(string) || options.Length == 0
        || options.Any(x => x.Value == null || string.IsNullOrWhiteSpace(x.NameLocKey))
        || options.Select(x => x.Value).Distinct().Count() != options.Length)) {
      throw new ArgumentException("Options require unique string values and localization keys.");
    }
    return new Dictionary<string, object> {
        ["kind"] = kind, ["modId"] = _modId, ["modNameLocKey"] = _modNameLocKey,
        ["id"] = RequiredId(id, nameof(id)), ["nameLocKey"] = Required(nameLocKey, nameof(nameLocKey)),
        ["type"] = type, ["resolve"] = resolve, ["percent"] = percent, ["min"] = min, ["max"] = max,
        ["optionValues"] = options?.Select(x => x.Value).ToArray(),
        ["optionNames"] = options?.Select(x => x.NameLocKey).ToArray(),
    };
  }

  void AddDeclaration(IDictionary<string, object> definition) {
    _declarations.Add(definition);
    if (Connect()) {
      PublishDeclarations();
    }
  }

  bool Connect() {
    if (_register != null) {
      return _isActive();
    }
    if (!_searched) {
      _searched = true;
      var endpoint = AppDomain.CurrentDomain.GetAssemblies()
          .Select(assembly => assembly.GetType(EndpointTypeName)).FirstOrDefault(type => type != null);
      var method = endpoint?.GetMethod(EndpointMethodName, BindingFlags.Public | BindingFlags.Static);
      if (method == null) {
        // This copied helper must not depend on Automation's logging utilities.
        UnityEngine.Debug.LogFormat(
            "[Automation API] Mod '{0}' supports Automation.{1}, but the endpoint was not found.",
            _modId, EndpointMethodName);
      }
      _connect = method == null ? null
          : (Func<IContainer, IDictionary<string, Delegate>>)method.CreateDelegate(
              typeof(Func<IContainer, IDictionary<string, Delegate>>));
    }
    var connection = _connect?.Invoke(_container);
    if (connection == null) {
      return false;
    }
    _register = (Action<IDictionary<string, object>>)connection["register"];
    _update = (Action<string, string, BaseComponent>)connection["update"];
    _isActive = (Func<bool>)connection["active"];
    return true;
  }

  void PublishDeclarations() {
    while (_published < _declarations.Count) {
      _register(_declarations[_published]);
      _published++;
    }
  }

  static string RequiredId(string value, string name) {
    if (!Regex.IsMatch(value ?? "", @"\A[A-Za-z][A-Za-z0-9]*\z")) {
      throw new ArgumentException(
          "ID must start with an ASCII letter and contain only ASCII letters and digits.", name);
    }
    return value;
  }

  static string Required(string value, string name) {
    return !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Value is required.", name);
  }
}
