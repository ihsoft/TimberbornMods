// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.Growing;
using Timberborn.TemplateInstantiation;

namespace IgorZ.TimberCommons.IrrigationSystem;

[Context("Game")]
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<BlockContaminationRangeEffect>().AsTransient();
    containerDefinition.Bind<GoodConsumingIrrigationTower>().AsTransient();
    containerDefinition.Bind<GrowthRateModifier>().AsTransient();
    containerDefinition.Bind<ManufactoryIrrigationTower>().AsTransient();
    containerDefinition.Bind<ModifyGrowableGrowthRangeEffect>().AsTransient();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<BlockContaminationRangeEffectSpec, BlockContaminationRangeEffect>();
    builder.AddDecorator<GoodConsumingIrrigationTowerSpec, GoodConsumingIrrigationTower>();
    builder.AddDecorator<Growable, GrowthRateModifier>();
    builder.AddDecorator<ManufactoryIrrigationTowerSpec, ManufactoryIrrigationTower>();
    builder.AddDecorator<ModifyGrowableGrowthRangeEffectSpec, ModifyGrowableGrowthRangeEffect>();
    return builder.Build();
  }
}