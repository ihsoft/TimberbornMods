// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.TemplateInstantiation;

namespace IgorZ.FillTheGap.Core;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<FillTheGapBuilding>().AsTransient();
    containerDefinition.Bind<PlatformReplacementService>().AsSingleton();
    containerDefinition.MultiBind<IBlockObjectValidator>().To<FillTheGapBlockObjectValidator>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<FillTheGapSpec, FillTheGapBuilding>();
    return builder.Build();
  }
}
