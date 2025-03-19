// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

[Context("Game")]
[Context("MapEditor")]
class CustomCursorServiceConfigurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<CustomCursorService>().AsSingleton();
  }
}
