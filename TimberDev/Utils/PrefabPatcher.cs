// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Text;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.Utils;

/// <summary>Helper class for managing prefab components.</summary>
static class PrefabPatcher {
  /// <summary>Adds a new component to prefab based in the filter condition.</summary>
  /// <param name="prefab">Prefab to add the component to.</param>
  /// <param name="checkDeps">Filter condition.</param>
  /// <param name="checkForDuplicates">Tell to verify if the component has already present in prefab.</param>
  /// <param name="onAdd">Called when a new component is added.</param>
  /// <typeparam name="T">type of the component to add.</typeparam>
  public static void AddComponent<T>(
      GameObject prefab, Func<GameObject, bool> checkDeps,
      bool checkForDuplicates = true,
      Action<T> onAdd = null) where T : BaseComponent {
    if (checkForDuplicates && prefab.GetComponent<T>() || !checkDeps(prefab)) {
      return;
    }
    DebugEx.Fine("Add component to prefab: name={0}, component={1}", prefab.name, typeof(T));
    var component = prefab.AddComponent<T>();
    onAdd?.Invoke(component);
  }

  /// <summary>Replaces an existing component with a new one.</summary>
  /// <remarks>
  /// Use it with care due to other components on the prefab may be expecting the old component to exist.
  /// </remarks>
  /// <param name="prefab">Prefab to replace the component on.</param>
  /// <param name="checkDeps">Filter condition.</param>
  /// <param name="onReplace">Callback that is called on the new instance creation.</param>
  /// <typeparam name="TSource">type of the component to remove.</typeparam>
  /// <typeparam name="TTarget">type of the component to add.</typeparam>
  public static void ReplaceComponent<TSource, TTarget>(GameObject prefab, Func<GameObject, bool> checkDeps = null,
                                                        Action<TSource, TTarget> onReplace = null)
      where TSource : BaseComponent where TTarget : BaseComponent {
    if (!prefab.GetComponent<TSource>()
        || prefab.GetComponent<TTarget>()
        || checkDeps != null && !checkDeps(prefab)) {
      return;
    }
    DebugEx.Fine("Replace component on prefab: name={0}, oldComponent={1}, newComponent={2}",
                 prefab.name, typeof(TSource), typeof(TTarget));
    var oldComponent = prefab.GetComponent<TSource>();
    var newComponent = prefab.AddComponent<TTarget>();
    onReplace?.Invoke(oldComponent, newComponent);
    Object.DestroyImmediate(oldComponent);
  }

  /// <summary>Helper filter class to check the existing and required components on prefab.</summary>
  public class RequiredComponentsDep {
    readonly Type[] _requiredComponents;

    /// <summary>Creates dependency from the types.</summary>
    public RequiredComponentsDep(params Type[] requiredComponents) {
      _requiredComponents = requiredComponents;
    }

    /// <summary>Checks if prefab has all the dependency classes.</summary>
    public bool Check(GameObject prefab) {
      var components = prefab.GetComponents<BaseComponent>().Select(x => x.GetType()).ToArray();
      return _requiredComponents.All(components.Contains);
    }
  }

  /// <summary>Dumps all Unity components on the game object.</summary>
  public static void DumpAllComponents(GameObject prefab) {
    DebugEx.Warning("*** Dumping components of prefab: name={0} ***", prefab.name);
    foreach (var component in prefab.GetComponents<BaseComponent>()) {
      DebugEx.Warning("  - {0}", component.GetType());
    }
  }

  /// <summary>Prints the hierarchy of the model on the game object.</summary>
  public static void PrintHierarchy(GameObject gameObject) {
    var items = new StringBuilder();
    PrintChildRecursive(items, gameObject.transform, 0);
    DebugEx.Warning("*** Hierarchy on {0}:\n{1}", gameObject.name, items.ToString());
  }

  static Transform PrintChildRecursive(StringBuilder sb, Transform transform, int depth) {
    for (var index = 0; index < transform.childCount; ++index) {
      var child = transform.GetChild(index);
      sb.Append(new string(' ', depth * 2)).Append("+ ").Append(child.name).Append("\n");
      PrintChildRecursive(sb, child, depth + 1);
    }
    return null;
  }
}
