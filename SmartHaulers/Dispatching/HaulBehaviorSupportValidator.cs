// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Reflection;
using Timberborn.Hauling;
using Timberborn.SingletonSystem;
using Timberborn.WorkSystem;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulBehaviorSupportValidator : ILoadableSingleton {
  static readonly BindingFlags BehaviorFieldFlags =
      BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

  static readonly Type HaulBehaviorProviderType = typeof(IHaulBehaviorProvider);
  static readonly Type WorkplaceBehaviorType = typeof(WorkplaceBehavior);

  public void Load() {
    foreach (var providerType in GetHaulBehaviorProviderTypes()) {
      ValidateProvider(providerType);
    }
  }

  static Type[] GetHaulBehaviorProviderTypes() {
    return AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(GetAssemblyTypes)
        .Where(type => !type.IsAbstract && HaulBehaviorProviderType.IsAssignableFrom(type))
        .ToArray();
  }

  static Type[] GetAssemblyTypes(Assembly assembly) {
    try {
      return assembly.GetTypes();
    } catch (ReflectionTypeLoadException e) {
      return e.Types.Where(type => type != null).ToArray();
    }
  }

  static void ValidateProvider(Type providerType) {
    var behaviorFields = providerType.GetFields(BehaviorFieldFlags)
        .Where(field => WorkplaceBehaviorType.IsAssignableFrom(field.FieldType))
        .ToList();
    if (behaviorFields.Count == 0) {
      throw new NotSupportedException(
          $"SmartHaulers cannot validate haul behavior provider {providerType.FullName}: no WorkplaceBehavior fields.");
    }
    foreach (var field in behaviorFields) {
      PossibleTransportOrderPlanner.EnsureSupported(field.FieldType, providerType);
    }
  }
}
