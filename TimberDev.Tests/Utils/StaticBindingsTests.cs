using System;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class StaticBindingsTests {
  public static void ConstructorStoresDependencyContainer() {
    var container = new TestContainer();
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);

    constructor.Invoke([container]);

    Assert.Equal(container, StaticBindings.DependencyContainer);
  }

  public static void ConfiguratorRegistersSingleton() {
    var configurator = new StaticBindingsConfigurator();
    var containerDefinition = new TestContainerDefinition();

    configurator.Configure(containerDefinition);

    Assert.Equal(1, containerDefinition.Bindings.Count);
    Assert.Equal(typeof(StaticBindings), containerDefinition.Bindings[0].Type);
    Assert.True(containerDefinition.Bindings[0].Singleton);
  }

  public static void ConfiguratorDeclaresExpectedContexts() {
    var contexts = typeof(StaticBindingsConfigurator)
        .GetCustomAttributes<Context>()
        .Select(attribute => attribute.Name)
        .OrderBy(name => name)
        .ToArray();

    Assert.Equal("Game", contexts[0]);
    Assert.Equal("Menu", contexts[1]);
    Assert.Equal(2, contexts.Length);
  }
}
