// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Utils;

/// <summary>Serializer that can handle the descendant classes.</summary>
/// <remarks>
/// <p>
/// This serializer stores the actual type info into the state and uses it during the load. Serializer for type
/// <typeparamref name="T"/> can load any type that is descendant of <typeparamref name="T"/>. To get the final
/// type, make the upcast from the loaded instance.
/// </p>
/// <p>
/// Even though the <typeparamref name="T"/> type can be abstract, the actual type that was serialized mustn't be
/// abstract and has to have a public default constructor to be loaded.
/// </p>
/// </remarks>
/// <typeparam name="T">the type of the base class. It can be abstract.</typeparam>
/// <seealso cref="DynamicClassSerializer{T}"/>
public sealed class DynamicClassSerializer<T> : IValueSerializer<T> where T : class, IGameSerializable {
  /// <summary>Property name that identifies the actual tape in the saved state.</summary>
  static readonly PropertyKey<string> TypeIdPropertyKey = new("TypeId");

  readonly bool _failFast;

  /// <summary>Creates the serializer.</summary>
  /// <param name="failFast">
  /// Indicates if the type loading errors must result into an exception. If set to <c>false</c>, then the errors will
  /// be logged, but instead of failing, a <c>null</c> value will be returned. This lets the clients handle the
  /// broken states.
  /// </param>
  public DynamicClassSerializer(bool failFast = true) {
    _failFast = failFast;
  }

  /// <inheritdoc/>
  public void Serialize(T value, IValueSaver valueSaver) {
    var objectSaver = valueSaver.AsObject();
    objectSaver.Set(TypeIdPropertyKey, value.GetType().FullName);
    value.SaveTo(objectSaver);
  }

  /// <inheritdoc/>
  public Obsoletable<T> Deserialize(IValueLoader valueLoader) {
    var objectLoader = valueLoader.AsObject();
    var savedTypeId = objectLoader.Get(TypeIdPropertyKey);
    var instance = MakeInstance(savedTypeId, _failFast);
    if (instance != null) {
      instance.LoadFrom(objectLoader);
    }
    return instance;
  }

  /// <summary>Creates an instance of the type <typeparamref name="T"/> from the provided type identifier.</summary>
  /// <exception cref="InvalidOperationException">if the type can't be created, or it is incompatible</exception>
  public static T MakeInstance(string typeId, bool failFast = true) {
    var objectType = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(typeId))
        .FirstOrDefault(t => t != null);
    string err = null;
    if (objectType == null) {
      err = $"Cannot find type for typeId: {typeId}";
    } else if (objectType.GetConstructor(Type.EmptyTypes) == null) {
      err = $"No default constructor in: {objectType}";
    } else if (!typeof(T).IsAssignableFrom(objectType)) {
      err = $"Incompatible types: {typeof(T)} is not assignable from {objectType}";
    }
    if (err != null) {
      if (failFast) {
        throw new InvalidOperationException(err);
      }
      DebugEx.Error(err);
      return null;
    }
    var instance = (T) Activator.CreateInstance(objectType);
    return instance;
  }
}