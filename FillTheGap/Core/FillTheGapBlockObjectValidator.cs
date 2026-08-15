// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlockSystem;
using Timberborn.Localization;

namespace IgorZ.FillTheGap.Core;

sealed class FillTheGapBlockObjectValidator : IBlockObjectValidator {
  const string InvalidPlacementLocKey = "Building.FillTheGap.InvalidPlacement";
  const string PlatformInsideTerrainBlockLocKey = "Building.FillTheGap.PlatformInsideTerrainBlock";

  readonly ILoc _loc;
  readonly PlatformReplacementService _platformReplacementService;

  public FillTheGapBlockObjectValidator(ILoc loc, PlatformReplacementService platformReplacementService) {
    _loc = loc;
    _platformReplacementService = platformReplacementService;
  }

  public bool IsValid(BlockObject blockObject, out string errorMessage) {
    errorMessage = string.Empty;
    if (!blockObject.IsPreview) {
      return true;
    }

    if (blockObject.HasComponent<FillTheGapSpec>()) {
      return IsFillTheGapValid(blockObject, out errorMessage);
    }

    if (_platformReplacementService.IsSupportedPlatform(blockObject)
        && _platformReplacementService.IntersectsUnfinishedFillTheGap(blockObject)) {
      errorMessage = _loc.T(PlatformInsideTerrainBlockLocKey);
      return false;
    }

    return true;
  }

  bool IsFillTheGapValid(BlockObject blockObject, out string errorMessage) {
    errorMessage = string.Empty;
    if (_platformReplacementService.TryGetPlatformContaining(blockObject.Coordinates, out var platform)) {
      if (_platformReplacementService.CanPlaceInsidePlatform(blockObject, platform)) {
        return true;
      }
    } else if (_platformReplacementService.CanPlaceAsOrdinaryTerrainBlock(blockObject)) {
      return true;
    }

    errorMessage = _loc.T(InvalidPlacementLocKey);
    return false;
  }
}
