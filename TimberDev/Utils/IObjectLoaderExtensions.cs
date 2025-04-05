// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Persistence;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace IgorZ.TimberDev.Utils;

public static class IObjectLoaderExtensions {
  public static bool? GetValueOrNullable(this IObjectLoader objectLoader, PropertyKey<bool> key) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : null;
  }

  public static int? GetValueOrNullable(this IObjectLoader objectLoader, PropertyKey<int> key) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : null;
  }

  public static float? GetValueOrNullable(this IObjectLoader objectLoader, PropertyKey<float> key) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : null;
  }

  public static string GetValueOrNullable(this IObjectLoader objectLoader, PropertyKey<string> key) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : null;
  }

  public static T GetValueOrNull<T>(
      this IObjectLoader objectLoader, PropertyKey<T> key, IValueSerializer<T> serializer) {
    if (!objectLoader.Has(key)) {
      return default;
    }
    return objectLoader.Get(key, serializer);
  }

  public static bool GetValueOrDefault(
      this IObjectLoader objectLoader, PropertyKey<bool> key, bool defaultValue = false) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : defaultValue;
  }

  public static int GetValueOrDefault(
      this IObjectLoader objectLoader, PropertyKey<int> key, int defaultValue = 0) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : defaultValue;
  }

  public static float GetValueOrDefault(
      this IObjectLoader objectLoader, PropertyKey<float> key, float defaultValue = 0) {
    return objectLoader.Has(key) ? objectLoader.Get(key) : defaultValue;
  }

  public static string GetValueOrDefault(
      this IObjectLoader objectLoader, PropertyKey<string> key, string defaultValue = "") {
    return objectLoader.Has(key) ? objectLoader.Get(key) : defaultValue;
  }

}
