using System;
using System.Collections.Generic;

namespace Bindito.Core;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class Context : Attribute {
  public string Name { get; }

  public Context(string name) {
    Name = name;
  }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class Inject : Attribute {
}

public interface IConfigurator {
  void Configure(IContainerDefinition containerDefinition);
}

public interface IContainer {
}

public interface IContainerDefinition {
  IBindingBuilder<T> Bind<T>();
}

public interface IBindingBuilder<T> {
  void AsSingleton();
}

public sealed class TestContainer : IContainer {
}

public sealed class TestContainerDefinition : IContainerDefinition {
  public readonly List<(Type Type, bool Singleton)> Bindings = new();

  public IBindingBuilder<T> Bind<T>() {
    return new TestBindingBuilder<T>(this);
  }

  sealed class TestBindingBuilder<T> : IBindingBuilder<T> {
    readonly TestContainerDefinition _containerDefinition;

    public TestBindingBuilder(TestContainerDefinition containerDefinition) {
      _containerDefinition = containerDefinition;
    }

    public void AsSingleton() {
      _containerDefinition.Bindings.Add((typeof(T), true));
    }
  }
}
