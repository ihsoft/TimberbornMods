using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.TemplateInstantiation;

namespace IgorZ.DualDistrictWarehouse;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DualDistrictWarehouse>().AsTransient();
    containerDefinition.Bind<DualDistrictWarehouseRegistry>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    HarmonyPatcher.ApplyPatch(PatchId, typeof(ResourceCountingServicePatch));
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<DualDistrictWarehouseSpec, DualDistrictWarehouse>();
    return builder.Build();
  }
}
