using System;

namespace Timberborn.BlueprintSystem;

public abstract record ComponentSpec {
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SerializeAttribute : Attribute {
}
