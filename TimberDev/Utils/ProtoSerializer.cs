// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.IO;
using ProtoBuf;
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
  // Add more Unity or custom types here if needed.
  static StringProtoSerializer() {
    RuntimeTypeModel.Default.Add(typeof(Vector2Int), false).Add("x", "y");
    RuntimeTypeModel.Default.Add(typeof(Vector3Int), false).Add("x", "y", "z");
    RuntimeTypeModel.Default.Add(typeof(Vector2), false).Add("x", "y");
    RuntimeTypeModel.Default.Add(typeof(Vector3), false).Add("x", "y", "z");
  }

  /// <summary>Serializes the protobuf object to a base64 string.</summary>
  public static string Serialize<T>(T obj) {
    using var stream = new MemoryStream();
    Serializer.Serialize(stream, obj);
    return Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length);
  }

  /// <summary>Deserializes the protobuf object from a base64 string.</summary>
  public static T Deserialize<T>(string text) {
    using var stream = new MemoryStream(Convert.FromBase64String(text));
    return Serializer.Deserialize<T>(stream);
  }
}
