using System.Collections.Generic;

namespace Timberborn.SingletonSystem;

public sealed class EventBus {
  public readonly List<object> RegisteredObjects = [];

  public void Register(object obj) {
    RegisteredObjects.Add(obj);
  }

  public void Unregister(object obj) {
    RegisteredObjects.Remove(obj);
  }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
public sealed class OnEventAttribute : System.Attribute {
}
