// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.IrrigationSystemUI {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<IrrigationTowerFragment>().AsSingleton();
    containerDefinition.Bind<GrowthRateModifierFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  /// <summary>UI for the irrigation related components.</summary>
  /// <remarks>The tower and boosted trees.</remarks>
  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly IrrigationTowerFragment _irrigationTowerFragment;
    readonly GrowthRateModifierFragment _growthRateModifierFragment;

    public EntityPanelModuleProvider(IrrigationTowerFragment irrigationTowerFragment,
                                     GrowthRateModifierFragment growthRateModifierFragment) {
      _irrigationTowerFragment = irrigationTowerFragment;
      _growthRateModifierFragment = growthRateModifierFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_irrigationTowerFragment);
      builder.AddBottomFragment(_growthRateModifierFragment);
      return builder.Build();
    }
  }
}

}
