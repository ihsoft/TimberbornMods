using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockObjectTools;
using Timberborn.TemplateInstantiation;

namespace IgorZ.DualDistrictStorage;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DualDistrictStorage>().AsTransient();
    containerDefinition.Bind<DualDistrictStorageRegistry>().AsSingleton();
    containerDefinition.Bind<AsymmetricDualDistrictStoragePlacementMarker>().AsTransient();
    containerDefinition.MultiBind<IBlockObjectPlacer>().To<AsymmetricDualDistrictStoragePlacer>().AsSingleton();
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    HarmonyPatcher.ApplyPatch(
        PatchId,
        typeof(ResourceCountingServicePatch),
        typeof(BuildingPlacerPatch),
        typeof(StockpileGoodPileVisualizerPatch),
        typeof(StockpilePlaneVisualizerPatch));
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<DualDistrictStorageSpec, DualDistrictStorage>();
    builder.AddDecorator<AsymmetricDualDistrictStoragePlacerSpec,
        AsymmetricDualDistrictStoragePlacementMarker>();
    return builder.Build();
  }
}
