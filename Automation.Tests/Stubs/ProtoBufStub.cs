using System;

namespace ProtoBuf;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ProtoContractAttribute : Attribute {
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ProtoMemberAttribute : Attribute {
  public ProtoMemberAttribute(int tag) {
  }
}
