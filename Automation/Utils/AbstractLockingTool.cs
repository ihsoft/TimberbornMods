// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.EntitySystem;
using Timberborn.PrefabSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Utils;

/// <summary>Tool that can select objects by user criteria.</summary>
/// <remarks>
/// On selection start, this tool checks the highlighted entity. If allowed for, it will be used to filter the other
/// entities during the selection. This allows user to select only specific entities.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class AbstractLockingTool : AbstractAreaSelectionTool {

  const string StartSelectingPromptLoc = "TimberDev_Utils.Tools.LockSelectTool.StartSelectingPrompt";
  const string StartObjectSelectingPromptLoc = "TimberDev_Utils.Tools.LockSelectTool.StartObjectSelectingPrompt";
  const string SelectingOneObjectLoc = "TimberDev_Utils.Tools.LockSelectTool.SelectingObject";
  const string SelectingNObjectsLoc = "TimberDev_Utils.Tools.LockSelectTool.SelectingNObjects";
  const string DescriptionHint = "TimberDev_Utils.Tools.LockSelectTool.DescriptionHint";

  #region Local fields and properties

  EntityBadgeService _entityBadgeService;

  #endregion

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = new[] { DescriptionHint };
    base.Initialize();
  }

  /// <inheritdoc/>
  public override string WarningText() {
    if (SelectionModeActive && LockedComponent != null) {
      var goodObjectsSelectedCount = SelectedObjects.Count(ObjectFilterExpression);
      return goodObjectsSelectedCount == 1
          ? Loc.T(SelectingOneObjectLoc, LockedEntityNiceName)
          : Loc.T(SelectingNObjectsLoc, LockedEntityNiceName, goodObjectsSelectedCount);
    }
    if (!IsShiftHeld || InputService.MouseOverUI || SelectionModeActive) {
      return "";
    }
    if (HighlightedBlockObject != null && CheckCanLockOnComponent(HighlightedBlockObject)) {
      return Loc.T(StartObjectSelectingPromptLoc, GetEntityNiceName(HighlightedBlockObject));
    }
    return Loc.T(StartSelectingPromptLoc);
  }

  #endregion

  #region AbstractAreaSelectionTool overries

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    return CheckIfSimilar(blockObject);
  }

  /// <inheritdoc/>
  protected override void OnSelectionModeChange(bool newMode) {
    base.OnSelectionModeChange(newMode);
    if (newMode) {
      if (!IsShiftHeld
          || HighlightedBlockObject == null
          || !CheckCanLockOnComponent(HighlightedBlockObject)) {
        return;
      }
      LockedComponent = HighlightedBlockObject;
      LockedPrefabName = HighlightedBlockObject.GetComponentFast<PrefabSpec>().Name;
      LockedEntityNiceName = GetEntityNiceName(HighlightedBlockObject);
    } else {
      LockedComponent = null;
      LockedPrefabName = null;
      LockedEntityNiceName = null;
    }
  }

  #endregion

  #region API

  /// <summary>The reference block object to use in the current selection session.</summary>
  /// <remarks>
  /// It is only set if user requested the appropriate mode and the object on mouse click passed the
  /// <see cref="CheckCanLockOnComponent"/> check.
  /// </remarks>
  /// <seealso cref="AbstractAreaSelectionTool.SelectionModeActive"/>
  protected BlockObject LockedComponent { get; private set; }

  /// <summary>Prefab name of the currently locked component.</summary>
  /// <seealso cref="LockedComponent"/>
  protected string LockedPrefabName { get; private set; }

  /// <summary>User-friendly name of the currently locked component.</summary>
  /// <seealso cref="LockedComponent"/>
  protected string LockedEntityNiceName { get; private set; }

  /// <summary>Tells if the block object is a good target for the locking.</summary>
  /// <param name="obj">The object to check. It is never <c>null</c>.</param>
  protected abstract bool CheckCanLockOnComponent(BlockObject obj);

  /// <summary>Tells if the block object is "similar" to the currently locked component".</summary>
  /// <remarks>
  /// By default, the prefab name is checked. If there is no locked component, then any object will be "similar". The
  /// descendants can extend or override this logic.</remarks>
  /// <param name="obj">The object to check. It's never <c>null</c>.</param>
  /// <seealso cref="LockedComponent"/>
  protected virtual bool CheckIfSimilar(BlockObject obj) {
    return LockedComponent == null || obj.GetComponentFast<PrefabSpec>().IsNamed(LockedPrefabName);
  }

  /// <summary>Returns a user-friendly localized name of the entity.</summary>
  /// <remarks>If entity name can't be obtained, then the prefab name is returned.</remarks>
  protected string GetEntityNiceName(BaseComponent obj) {
    string niceName;
    if (obj.TryGetComponentFast<EntityComponent>(out var component)) {
      niceName = _entityBadgeService.GetEntityName(component);
    } else {
      DebugEx.Error("Cannot get entity for: {0}", obj);
      niceName = obj.GetComponentFast<PrefabSpec>().Name;
    }
    return niceName;
  }

  #endregion

  #region Local methods

  /// <summary>Injects the dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(EntityBadgeService entityBadgeService) {
    _entityBadgeService = entityBadgeService;
  }

  #endregion
}