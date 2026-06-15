using System;
using System.IO;
using System.Text.Json;

namespace ProtoBuf.Meta;

public sealed class RuntimeTypeModel {
  public static RuntimeTypeModel Create() {
    return new RuntimeTypeModel();
  }

  public MetaType Add(Type type) {
    return new MetaType(type);
  }

  public void Serialize<T>(Stream stream, T obj) {
    JsonSerializer.Serialize(stream, obj);
  }

  public T Deserialize<T>(Stream stream) {
    return JsonSerializer.Deserialize<T>(stream);
  }
}

public sealed class MetaType {
  public Type Type { get; }
  public string[] MemberNames { get; private set; }

  public MetaType(Type type) {
    Type = type;
  }

  public MetaType Add(string[] memberNames) {
    MemberNames = memberNames;
    return this;
  }
}
