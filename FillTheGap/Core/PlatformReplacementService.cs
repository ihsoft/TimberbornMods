// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Goods;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using UnityEngine;

namespace IgorZ.FillTheGap.Core;

sealed class PlatformReplacementService : ILoadableSingleton {
  static readonly Dictionary<string, string> TerrainBlockTemplateNames = new() {
      ["FillTheGap.Folktails"] = "TerrainBlock.Folktails",
      ["FillTheGap.IronTeeth"] = "TerrainBlock.IronTeeth",
  };

  readonly BlockValidator _blockValidator;
  readonly IBlockService _blockService;
  readonly TemplateNameMapper _templateNameMapper;

  public PlatformReplacementService(
      BlockValidator blockValidator, IBlockService blockService, TemplateNameMapper templateNameMapper) {
    _blockValidator = blockValidator;
    _blockService = blockService;
    _templateNameMapper = templateNameMapper;
  }

  public void Load() {
  }

  internal bool ShouldPropagateDeletionAbove(BlockObject fillTheGapBlock) {
    return !TryGetPlatformContaining(fillTheGapBlock.Coordinates, out _);
  }

  internal bool CanDeleteIndependently(BlockObject fillTheGapBlock) {
    return TryGetPlatformContaining(fillTheGapBlock.Coordinates, out _);
  }

  internal bool HasCompletedPlatformSurfaceAbove(Vector3Int coordinates) {
    return TryGetPlatformContaining(coordinates, out var platform)
        && TryGetDefinition(platform, out var definition)
        && PlatformReplacementRules.HasSurfaceAbove(platform.Coordinates.z, definition.Height, coordinates.z);
  }

  public bool TryGetPlatformContaining(Vector3Int coordinates, out BlockObject platform) {
    foreach (var candidate in _blockService.GetObjectsAt(coordinates)) {
      if (!candidate.IsFinished || !TryGetDefinition(candidate, out var definition)) {
        continue;
      }

      if (PlatformReplacementRules.ContainsLevel(candidate.Coordinates.z, definition.Height, coordinates.z)) {
        platform = candidate;
        return true;
      }
    }

    platform = null;
    return false;
  }

  public bool IsSupportedPlatform(BlockObject blockObject) {
    return TryGetDefinition(blockObject, out _);
  }

  public bool IntersectsUnfinishedFillTheGap(BlockObject platformPreview) {
    foreach (var coordinates in platformPreview.PositionedBlocks.GetAllCoordinates()) {
      foreach (var candidate in _blockService.GetObjectsAt(coordinates)) {
        if (candidate.IsUnfinished && candidate.HasComponent<FillTheGapSpec>()) {
          return true;
        }
      }
    }

    return false;
  }

  public bool CanPlaceInsidePlatform(BlockObject fillTheGapBlock, BlockObject platform) {
    return TryGetDefinition(platform, out var definition)
        && HasOnlySupportedOccupants(fillTheGapBlock, platform, definition)
        && HasReplacementCost(platform, definition);
  }

  public bool CanConvert(BlockObject fillTheGapBlock, BlockObject platform) {
    if (fillTheGapBlock.Coordinates != platform.Coordinates) {
      return false;
    }

    return CanPlaceInsidePlatform(fillTheGapBlock, platform);
  }

  public bool CanPlaceAsOrdinaryTerrainBlock(BlockObject fillTheGapBlock) {
    var templateName = fillTheGapBlock.GetComponent<TemplateSpec>().TemplateName;
    if (!TerrainBlockTemplateNames.TryGetValue(templateName, out var terrainBlockTemplateName)) {
      return false;
    }

    var terrainBlockTemplate = _templateNameMapper.GetTemplate(terrainBlockTemplateName).GetSpec<BlockObjectSpec>();
    return _blockValidator.BlocksValid(terrainBlockTemplate, fillTheGapBlock.Placement);
  }

  public BlockObjectSpec GetReplacementTemplate(BlockObject platform) {
    if (!TryGetDefinition(platform, out var definition)) {
      return null;
    }

    return definition.ReplacementTemplateName == null ? null : GetReplacementTemplate(definition);
  }

  public void RetainReplacementCost(BlockObject platform, BlockObjectSpec replacementTemplate) {
    if (replacementTemplate == null) {
      return;
    }

    var inventory = platform.GetComponent<ConstructionSite>().Inventory;
    foreach (var good in replacementTemplate.GetSpec<BuildingSpec>().BuildingCost) {
      inventory.TakeExisting(new GoodAmount(good.Id, good.Amount));
    }
  }

  bool HasOnlySupportedOccupants(
      BlockObject fillTheGapBlock, BlockObject platform, SupportedPlatformDefinition definition) {
    for (var z = 0; z < definition.Height; z++) {
      var coordinates = platform.Coordinates + new Vector3Int(0, 0, z);
      var path = z == 0 ? _blockService.GetPathObjectAt(coordinates) : null;
      foreach (var candidate in _blockService.GetObjectsAt(coordinates)) {
        if (candidate != platform
            && candidate != fillTheGapBlock
            && candidate != path
            && !candidate.Overridable
            && !candidate.HasComponent<FillTheGapSpec>()) {
          return false;
        }
      }
    }

    return true;
  }

  bool HasReplacementCost(BlockObject platform, SupportedPlatformDefinition definition) {
    if (definition.ReplacementTemplateName == null) {
      return true;
    }

    var constructionSite = platform.GetComponent<ConstructionSite>();
    var replacementCost = GetReplacementTemplate(definition).GetSpec<BuildingSpec>().BuildingCost;
    foreach (var good in replacementCost) {
      if (constructionSite.Inventory.AmountInStock(good.Id) < good.Amount) {
        return false;
      }
    }
    return true;
  }

  BlockObjectSpec GetReplacementTemplate(SupportedPlatformDefinition definition) {
    return _templateNameMapper.GetTemplate(definition.ReplacementTemplateName).GetSpec<BlockObjectSpec>();
  }

  static bool TryGetDefinition(BlockObject platform, out SupportedPlatformDefinition definition) {
    var templateName = platform.GetComponent<TemplateSpec>().TemplateName;
    return PlatformReplacementRules.TryGetDefinition(templateName, out definition);
  }
}
