// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using ProtoBuf.Meta;
using UnityEngine;

namespace IgorZ.TimberDev.Utils;

/// <summary>Serializer for the protobuf objects.</summary>
/// <remarks>
/// Requires "protobuf-net" package. Verify the specific version in the game DLLs. As of game `v0.7.8.9` it is
/// `protobuf-net v2.4`.
/// </remarks>
/// <example>
/// Declare the protobuf object with the <c>ProtoContract</c> attribute and the fields with the <c>ProtoMember</c>
/// attribute.
/// <code><![CDATA[
/// [ProtoContract]
/// record struct MyData {
///   [ProtoMember(1)]
///   public Vector2Int[] Positions { get; init; }
/// }
/// ]]></code>
///
/// Then you can serialize and deserialize the object using the <c>StringProtoSerializer</c>.
/// <code><![CDATA[
/// var obj = new MyData { Positions = [new Vector2Int(1, 2), new Vector2Int(3, 4)] };
/// var text = StringProtoSerializer.Serialize(obj);
/// var deserialized = StringProtoSerializer.Deserialize<MyData>(text);
/// ]]></code>
/// </example>
public static class StringProtoSerializer {
  /// <summary>Serializes the protobuf object to a base64 string.</summary>
  public static string Serialize<T>(T obj) {
    using var stream = new MemoryStream();
    GetRuntimeTypeModel().Serialize(stream, obj);
    return Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length);
  }

  /// <summary>Deserializes the protobuf object from a base64 string.</summary>
  public static T Deserialize<T>(string text) {
    using var stream = new MemoryStream(Convert.FromBase64String(text));
    return GetRuntimeTypeModel().Deserialize<T>(stream);
  }

  /// <summary>Adds a type to the serializer with the specified member names.</summary>
  public static void AddType<T>(string[] memberNames) {
    if (TypeToProtoMemberNames.TryGetValue(typeof(T), out var existingNames)) {
      if (existingNames.Length != memberNames.Length) {
        throw new InvalidDataException($"Type {typeof(T)} already registered with different member names.");
      }
      for (int i = 0; i < existingNames.Length; i++) {
        if (existingNames[i] != memberNames[i]) {
          throw new InvalidDataException($"Type {typeof(T)} already registered with different member names.");
        }
      }
      return;
    }
    TypeToProtoMemberNames[typeof(T)] = memberNames;
    _localRuntimeTypeModel = null;
  }

  /// <summary>Standard Unity types.</summary>
  static readonly Dictionary<Type, string[]> TypeToProtoMemberNames = new() {
      {typeof(Vector2Int), ["x", "y"]},
      {typeof(Vector3Int), ["x", "y", "z"]},
      {typeof(Vector2), ["x", "y"]},
      {typeof(Vector3), ["x", "y", "z"]},
  };

  static RuntimeTypeModel GetRuntimeTypeModel() {
    if (_localRuntimeTypeModel == null) {
      _localRuntimeTypeModel = RuntimeTypeModel.Create();
      foreach (var types in TypeToProtoMemberNames) {
        _localRuntimeTypeModel.Add(types.Key).Add(types.Value);
      }
    }
    return _localRuntimeTypeModel;
  }
  static RuntimeTypeModel _localRuntimeTypeModel;
}
