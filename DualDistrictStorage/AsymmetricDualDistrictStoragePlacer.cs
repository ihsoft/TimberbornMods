using System.Collections.Generic;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BuildingTools;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.InputSystem;
using Timberborn.LinkedBuildingSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystemUI;

namespace IgorZ.DualDistrictStorage;

sealed class AsymmetricDualDistrictStoragePlacer : IBlockObjectPlacer {
  const string PlaceFinishedKey = "PlaceFinished";

  readonly BuildingCostSectionProvider _buildingCostSectionProvider;
  readonly ConstructionFactory _constructionFactory;
  readonly InputService _inputService;
  readonly IEnumerable<ISectionProvider> _sectionProviders;
  readonly TemplateNameMapper _templateNameMapper;

  public AsymmetricDualDistrictStoragePlacer(
      BuildingCostSectionProvider buildingCostSectionProvider,
      ConstructionFactory constructionFactory,
      InputService inputService,
      IEnumerable<ISectionProvider> sectionProviders,
      TemplateNameMapper templateNameMapper) {
    _buildingCostSectionProvider = buildingCostSectionProvider;
    _constructionFactory = constructionFactory;
    _inputService = inputService;
    _sectionProviders = sectionProviders;
    _templateNameMapper = templateNameMapper;
  }

  public bool CanHandle(BlockObjectSpec template) {
    return template.HasSpec<AsymmetricDualDistrictStoragePlacerSpec>();
  }

  public void Describe(BlockObjectTool tool, ToolDescription.Builder builder, Preview preview) {
    if (_buildingCostSectionProvider.TryGetSection(preview, out var costSection)) {
      builder.AddExternalSection(costSection);
    }
    foreach (var sectionProvider in _sectionProviders) {
      if (sectionProvider.TryGetSection(preview, out var section)) {
        builder.AddSection(section);
      }
    }
  }

  public void Place(EntitySetup.Builder entitySetupBuilder, Placement placement) {
    var placerSpec = entitySetupBuilder.Template.GetSpec<AsymmetricDualDistrictStoragePlacerSpec>();
    var narrowTemplate = _templateNameMapper.GetTemplate(placerSpec.NarrowTemplateName);
    var wideTemplate = _templateNameMapper.GetTemplate(placerSpec.WideTemplateName);
    var narrowPlacement = placement;
    var wideCoordinates = placement.Coordinates
        + placement.Orientation.Transform(new UnityEngine.Vector3Int(2, 2, 0));
    var widePlacement = new Placement(wideCoordinates, placement.Orientation.Flip(), placement.FlipMode);
    var placeFinished = _inputService.IsKeyHeld(PlaceFinishedKey)
        || entitySetupBuilder.Template.GetSpec<BuildingSpec>().PlaceFinished;

    var narrow = Create(narrowTemplate, narrowPlacement, placeFinished);
    var wide = Create(wideTemplate, widePlacement, placeFinished);
    var narrowLinkedBuilding = narrow.GetComponent<LinkedBuilding>();
    var wideLinkedBuilding = wide.GetComponent<LinkedBuilding>();
    narrowLinkedBuilding.LinkBuilding(wideLinkedBuilding);
    wideLinkedBuilding.LinkBuilding(narrowLinkedBuilding);
  }

  BlockObject Create(TemplateSpec template, Placement placement, bool placeFinished) {
    var builder = new EntitySetup.Builder(template.Blueprint);
    return placeFinished
        ? _constructionFactory.CreateAsFinished(builder, placement)
        : _constructionFactory.CreateAsUnfinished(builder, placement);
  }
}
