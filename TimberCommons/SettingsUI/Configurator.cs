// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.EntityPanelSystem;

namespace IgorZ.TimberCommons.SettingsUI;

// Uncomment to be able to change settings without game reload. 
//[Context("Game")]
sealed class Configurator : IConfigurator {
  class EntityPanelModuleProvider(DistrictCenterFragment districtCenterFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddDiagnosticFragment(districtCenterFragment);
      return builder.Build();
    }
  }

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DistrictCenterFragment>().AsSingleton();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
  }
}
