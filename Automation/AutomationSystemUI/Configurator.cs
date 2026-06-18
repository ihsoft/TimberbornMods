// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.TimberDev.Utils;
using Timberborn.EntityPanelSystem;

namespace IgorZ.Automation.AutomationSystemUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<GameAutomationConflictDetector>().AsSingleton();
    containerDefinition.Bind<GameAutomationConflictGuardService>().AsSingleton();
    containerDefinition.Bind<AutomationFragment>().AsSingleton();
    containerDefinition.Bind<CopyRulesTool>().AsSingleton();
    containerDefinition.Bind<RulesUIHelper>().AsTransient();
    containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    HarmonyPatcher.ApplyPatch(PatchId, typeof(TransmitterSelectorPatch));
  }

  sealed class EntityPanelModuleProvider(AutomationFragment automationFragment) : IProvider<EntityPanelModule> {
    public EntityPanelModule Get() {
      var builder = new EntityPanelModule.Builder();
      builder.AddBottomFragment(automationFragment);
      return builder.Build();
    }
  }
}
