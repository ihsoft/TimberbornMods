// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.TemplateInstantiation;

namespace IgorZ.TimberCommons.Stockpiles;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<GoodAmountTransformHeight>().AsTransient();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<GoodAmountTransformHeightSpec, GoodAmountTransformHeight>();
    builder.AddDecorator<MultiGoodAmountTransformHeightSpec, GoodAmountTransformHeight>();
    return builder.Build();
  }
}
