// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

[Context("Game")]
sealed class Configurator : IConfigurator {

  /// <inheritdoc/>
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AdjustableWaterOutputFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }

  sealed class EntityPanelModuleProvider : IProvider<EntityPanelModule> {
    readonly AdjustableWaterOutputFragment _adjustableWaterOutputFragment;

    EntityPanelModuleProvider(AdjustableWaterOutputFragment adjustableWaterOutputFragment) {
     _adjustableWaterOutputFragment = adjustableWaterOutputFragment;
    }

    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(_adjustableWaterOutputFragment);
      return builder.Build();
    }
  }
}
