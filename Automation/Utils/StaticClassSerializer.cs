// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Persistence;

namespace IgorZ.Automation.Utils;

/// <summary>Simple generic serializer for a class that implements <see cref="IGameSerializable"/>.</summary>
/// <remarks>
/// <p>
/// This serializer simply passes the control to the serializable object. On load, an instance of type
/// <typeparamref name="T"/> is created and loaded, so to load descendant classes, a specialized serializer is
/// needed for each type.
/// </p>
/// <p>
/// In a general case, this serializer should only be used on the sealed classes. If a class can be extended, then it
/// may be a more reliable way to use <see cref="DynamicClassSerializer{T}"/>.
/// </p>
/// </remarks>
/// <typeparam name="T">the type of the class</typeparam>
/// <seealso cref="DynamicClassSerializer{T}"/>
public sealed class StaticClassSerializer<T> : IValueSerializer<T> where T : IGameSerializable, new() {
  /// <inheritdoc/>
  public void Serialize(T value, IValueSaver objectSaver) {
    value.SaveTo(objectSaver.AsObject());
  }

  /// <inheritdoc/>
  public Obsoletable<T> Deserialize(IValueLoader objectLoader) {
    var instance = new T();
    instance.LoadFrom(objectLoader.AsObject());
    return instance;
  }
}