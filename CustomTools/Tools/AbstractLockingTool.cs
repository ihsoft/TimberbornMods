// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.EntitySystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace IgorZ.CustomTools.Tools;

/// <summary>Tool that can select objects by user criteria.</summary>
/// <remarks>
/// On selection start, this tool checks the highlighted entity. If allowed for, it will be used to filter the other
/// entities during the selection. This allows user to select only specific entities.
/// </remarks>
public abstract class AbstractLockingTool : AbstractAreaSelectionTool {

  const string StartSelectingPromptLoc = "IgorZ.CustomTools.LockSelectTool.StartSelectingPrompt";
  const string StartObjectSelectingPromptLoc = "IgorZ.CustomTools.LockSelectTool.StartObjectSelectingPrompt";
  const string SelectingOneObjectLoc = "IgorZ.CustomTools.LockSelectTool.SelectingObject";
  const string SelectingNObjectsLoc = "IgorZ.CustomTools.LockSelectTool.SelectingNObjects";
  const string DescriptionHint = "IgorZ.CustomTools.LockSelectTool.DescriptionHint";

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    DescriptionBullets = [Loc.T(DescriptionHint)];
    base.Initialize();
  }

  /// <inheritdoc/>
  public override string GetWarningText() {
    if (SelectionModeActive && LockedComponent) {
      var goodObjectsSelectedCount = SelectedObjects.Count(ObjectFilterExpression);
      return goodObjectsSelectedCount == 1
          ? Loc.T(SelectingOneObjectLoc, LockedEntityNiceName)
          : Loc.T(SelectingNObjectsLoc, LockedEntityNiceName, goodObjectsSelectedCount);
    }
    if (!IsShiftHeld || InputService.MouseOverUI || SelectionModeActive) {
      return "";
    }
    if (HighlightedBlockObject && CheckCanLockOnComponent(HighlightedBlockObject)) {
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
      if (!IsShiftHeld || !HighlightedBlockObject || !CheckCanLockOnComponent(HighlightedBlockObject)) {
        return;
      }
      LockedComponent = HighlightedBlockObject;
      LockedPrefabName = HighlightedBlockObject.Name;
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
  /// <param name="obj">The object to check. It is never <c>null</c>.</param>
  /// <seealso cref="LockedComponent"/>
  protected virtual bool CheckIfSimilar(BlockObject obj) {
    return !LockedComponent || obj.Name == LockedPrefabName;
  }

  /// <summary>Returns a user-friendly localized name of the entity.</summary>
  /// <remarks>If entity name can't be obtained, then the prefab name is returned.</remarks>
  protected string GetEntityNiceName(BaseComponent obj) {
    string niceName;
    if (obj.TryGetComponent<LabeledEntity>(out var component)) {
      niceName = component.DisplayName;
    } else {
      DebugEx.Error("Cannot get entity for: {0}", obj);
      niceName = obj.Name;
    }
    return niceName;
  }

  #endregion
}
