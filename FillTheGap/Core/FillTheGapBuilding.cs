// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.DeconstructionSystem;
using Timberborn.EntitySystem;
using UnityEngine;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.FillTheGap.Core;

sealed class FillTheGapBuilding : BaseComponent, IAwakableComponent, IFinishedStateListener,
    IConstructionFinishBlocker {
  readonly BlockValidator _blockValidator;
  readonly ConstructionFactory _constructionFactory;
  readonly EntityService _entityService;
  readonly IBlockService _blockService;
  readonly PlatformReplacementService _platformReplacementService;

  BlockObject _blockObject;
  BlockObjectSpec _replacementTemplate;
  Placement _replacementPlacement;

  public bool IsFinishBlocked {
    get {
      if (!_platformReplacementService.TryGetPlatformContaining(_blockObject.Coordinates, out var platform)) {
        return false;
      }
      return !_platformReplacementService.CanConvert(_blockObject, platform);
    }
  }

  public FillTheGapBuilding(
      BlockValidator blockValidator, ConstructionFactory constructionFactory, EntityService entityService,
      IBlockService blockService, PlatformReplacementService platformReplacementService) {
    _blockValidator = blockValidator;
    _constructionFactory = constructionFactory;
    _entityService = entityService;
    _blockService = blockService;
    _platformReplacementService = platformReplacementService;
  }

  public void Awake() {
    _blockObject = GetComponent<BlockObject>();
    GetComponent<DeleteOnFinishConstructionSite>().Deleted += OnDeleted;
  }

  public void OnEnterFinishedState() {
    if (!_platformReplacementService.TryGetPlatformContaining(_blockObject.Coordinates, out var platform)) {
      return;
    }

    _replacementTemplate = _platformReplacementService.GetReplacementTemplate(platform);
    _replacementPlacement = new Placement(
        platform.Coordinates + Vector3Int.forward, platform.Orientation, platform.FlipMode);
    _platformReplacementService.RetainReplacementCost(platform, _replacementTemplate);
    DeletePath();
    _entityService.Delete(platform);
  }

  public void OnExitFinishedState() {
  }

  void OnDeleted(object sender, EventArgs e) {
    if (_replacementTemplate == null) {
      return;
    }

    if (!_blockValidator.BlocksValid(_replacementTemplate, _replacementPlacement)) {
      HostedDebugLog.Error(this, "Could not place shortened platform at {0}.", _replacementPlacement.Coordinates);
      return;
    }

    var entitySetupBuilder = new EntitySetup.Builder(_replacementTemplate.Blueprint);
    _constructionFactory.CreateAsFinished(entitySetupBuilder, _replacementPlacement);
  }

  void DeletePath() {
    var path = _blockService.GetPathObjectAt(_blockObject.Coordinates);
    if (path == null) {
      return;
    }

    path.GetComponent<Deconstructible>().DisableDeconstruction();
    _entityService.Delete(path);
  }
}
