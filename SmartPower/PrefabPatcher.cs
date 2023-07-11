// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.BaseComponentSystem;
using UnityDev.LogUtils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SmartPower {

/// <summary>Helper class for managing prefab components.</summary>
/// <remarks>
/// Call the patcher methods from the <c>InGame</c> configurator. Each game load refreshes the prefabs, so they need to
/// patched on every game load.
/// </remarks>
public static class PrefabPatcher {
  /// <summary>Adds a new component to prefab based in the filter condition.</summary>
  /// <param name="prefab">Prefab to add the component to.</param>
  /// <param name="requiredComponents">A set of components that must exist on the prefab.</param>
  /// <typeparam name="T">type of the component to add.</typeparam>
  public static void AddComponent<T>(GameObject prefab, Type[] requiredComponents) where T : BaseComponent {
    if (prefab.GetComponent<T>() != null) {
      return;
    }
    var components = prefab.GetComponents<BaseComponent>().Select(x => x.GetType()).ToArray();
    if (!requiredComponents.All(components.Contains)) {
      return;
    }
    DebugEx.Fine("Add component to prefab: name={0}, component={1}", prefab.name, typeof(T));
    prefab.AddComponent<T>();
  }

  /// <summary>Replaces an existing component with a new one.</summary>
  /// <remarks>
  /// Use it with care due to other components on the prefab may be expecting the old component to exist.
  /// </remarks>
  /// <param name="prefab">Prefab to replace the component on.</param>
  /// <typeparam name="TSource">type of the component to remove.</typeparam>
  /// <typeparam name="TTarget">type of the component to add.</typeparam>
  public static void ReplaceComponent<TSource, TTarget>(GameObject prefab)
      where TSource : BaseComponent where TTarget : BaseComponent {
    if (prefab.GetComponent<TTarget>() != null || prefab.GetComponent<TSource>() == null) {
      return;
    }
    DebugEx.Fine("Replace component on prefab: name={0}, oldComponent={1}, newComponent={2}",
                 prefab.name, typeof(TSource), typeof(TTarget));
    Object.DestroyImmediate(prefab.GetComponent<TSource>());
    prefab.AddComponent<TTarget>();
  }
}

}
