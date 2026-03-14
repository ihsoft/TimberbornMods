// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ObjectsCache<TObject> where TObject : class {
  public TObject GetOrAdd<T>(T key, Func<T, TObject> createFunc) where T : notnull {
    if (!_cache.TryGetValue(key, out var obj)) {
      obj = createFunc(key);
      _cache[key] = obj;
    }
    return obj;
  }

  public TObject GetOrAdd<T1, T2>(T1 key1, T2 key2, Func<T1, T2, TObject> createFunc)
      where T1 : notnull where T2 : notnull {
    var key = (key1, key2);
    if (!_cache.TryGetValue(key, out var obj)) {
      obj = createFunc(key1, key2);
      _cache[key] = obj;
    }
    return obj;
  }

  public TObject GetOrAdd<T1, T2, T3>(T1 key1, T2 key2, T3 key3, Func<T1, T2, T3, TObject> createFunc)
      where T1 : notnull where T2 : notnull {
    var key = (key1, key2, key3);
    if (!_cache.TryGetValue(key, out var obj)) {
      obj = createFunc(key1, key2, key3);
      _cache[key] = obj;
    }
    return obj;
  }

  public TObject GetOrAdd<T1, T2, T3, T4>(T1 key1, T2 key2, T3 key3, T4 key4, Func<T1, T2, T3, T4, TObject> createFunc)
      where T1 : notnull where T2 : notnull {
    var key = (key1, key2, key3, key4);
    if (!_cache.TryGetValue(key, out var obj)) {
      obj = createFunc(key1, key2, key3, key4);
      DebugEx.Fine("Creating cached object: {0}: {1}", key, obj);
      _cache[key] = obj;
    }
    return obj;
  }

  readonly Dictionary<object, TObject> _cache = new();
}
