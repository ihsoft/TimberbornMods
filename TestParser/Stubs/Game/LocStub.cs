using System.Collections.Generic;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;

namespace TestParser.Stubs.Game;

class LocStub : ILoc {
  Dictionary<string, string> _localization = new() {
      { "IgorZ.Automation.Scripting.Expressions.AndOperator", "AND" },
      { "IgorZ.Automation.Scripting.Expressions.OrOperator", "OR" },
      { "IgorZ.Automation.Scriptable.Signals.Signal.Get", "Signal({0})" },
      { "IgorZ.Automation.Scriptable.Signals.Action.Set", "SetSignal({0})" },
      { "IgorZ.Automation.Scriptable.Debug.Action.Log", "write to logs: '{0}'" },
  };

  readonly Dictionary<string, TextLocalizationWrapper> _localizationCache = new();

  public void Initialize(Dictionary<string, string> localization) {
    _localizationCache.Clear();
    _localization = localization;
  }

  public IEnumerable<string> GetRawTexts() {
    return _localization.Values;
  }

  public string T(string key) {
    if (key != null && _localization.TryGetValue(key, out var value)) {
      return value;
    }
    DebugEx.Error("The given key " + key + " was not present in the dictionary.");
    return key;
  }

  public string T<T1>(string key, T1 param1) {
    return T<T1, object, object>(key, param1, null, null);
  }

  public string T<T1, T2>(string key, T1 param1, T2 param2) {
    return T<T1, T2, object>(key, param1, param2, null);
  }

  public string T<T1, T2, T3>(string key, T1 param1, T2 param2, T3 param3) {
    if (!_localizationCache.ContainsKey(key)) {
      _localizationCache[key] = new TextLocalizationWrapper(T(key));
    }
    return _localizationCache[key].GetText(this, param1, param2, param3);
  }

  public string T(Phrase phrase) {
    throw new System.NotImplementedException();
  }
  public string T<T1>(Phrase phrase, T1 param1) {
    throw new System.NotImplementedException();
  }
  public string T<T1, T2>(Phrase phrase, T1 param1, T2 param2) {
    throw new System.NotImplementedException();
  }
  public string T<T1, T2, T3>(Phrase phrase, T1 param1, T2 param2, T3 param3) {
    throw new System.NotImplementedException();
  }
}
