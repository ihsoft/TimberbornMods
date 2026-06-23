using System;

namespace Timberborn.SingletonSystem;

public interface IPostLoadableSingleton {
  void PostLoad();
}

[AttributeUsage(AttributeTargets.Method)]
public class OnEventAttribute : Attribute {
}

public class EventBus {
  public object RegisteredObject { get; private set; }

  public void Register(object listener) {
    RegisteredObject = listener;
  }
}
