// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils.Utils {

/// <summary>Helper class for managing prefab components.</summary>
public static class PrefabPatcher {
  /// <summary>Adds a new component to prefab based in the filter condition.</summary>
  /// <param name="prefab">Prefab to add the component to.</param>
  /// <param name="checkDeps">Filter condition.</param>
  /// <typeparam name="T">type of the component to add.</typeparam>
  public static void AddComponent<T>(GameObject prefab, Func<GameObject, bool> checkDeps) where T : BaseComponent {
    if (prefab.GetComponent<T>() != null || !checkDeps(prefab)) {
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
  /// <param name="checkDeps">Filter condition.</param>
  /// <typeparam name="TSource">type of the component to remove.</typeparam>
  /// <typeparam name="TTarget">type of the component to add.</typeparam>
  public static void ReplaceComponent<TSource, TTarget>(GameObject prefab, Func<GameObject, bool> checkDeps)
      where TSource : BaseComponent where TTarget : BaseComponent {
    if (prefab.GetComponent<TTarget>() != null || prefab.GetComponent<TSource>() == null || !checkDeps(prefab)) {
      return;
    }
    DebugEx.Fine("Replace component on prefab: name={0}, oldComponent={1}, newComponent={2}",
                 prefab.name, typeof(TSource), typeof(TTarget));
    Object.DestroyImmediate(prefab.GetComponent<TSource>());
    prefab.AddComponent<TTarget>();
  }

  /// <summary>Helper filter class to check the existing and required components on prefab.</summary>
  public class RequiredComponentsDep {
    readonly Type[] _requiredComponents;
    public RequiredComponentsDep(params Type[] requiredComponents) {
      _requiredComponents = requiredComponents;
    }
    public bool Check(GameObject prefab) {
      var components = prefab.GetComponents<BaseComponent>().Select(x => x.GetType()).ToArray();
      return _requiredComponents.All(components.Contains);
    }
  }
}

}
