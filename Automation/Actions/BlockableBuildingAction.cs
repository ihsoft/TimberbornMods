// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using TimberApi.DependencyContainerSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using UnityEngine;

namespace IgorZ.Automation.Actions;

/// <summary>Blocks the building when the state triggers. Optionally, adds a normal status.</summary>
public sealed class BlockableBuildingAction : AutomationActionBase {

  const string BlockDescriptionLocKey = "IgorZ.Automation.BlockableBuildingAction.BlockDescription";
  const string UnblockDescriptionLocKey = "IgorZ.Automation.BlockableBuildingAction.UnblockDescription";

  #region API
  // ReSharper disable MemberCanBePrivate.Global
  // ReSharper disable UnusedMember.Global

  /// <summary>All actions that this rule can perform.</summary>
  public enum ActionKindEnum {
    /// <summary>Block building and show status.</summary>
    Block,
    /// <summary>Unblock building and hide status if any.</summary>
    Unblock,
  }

  /// <summary>Specifies what this action should do to the blockable building.</summary>
  public ActionKindEnum ActionKind { get; private set;  }

  /// <summary>This is an identifier to bind the block/unblock activity to.</summary>
  /// <remarks>
  /// It's important to keep consistency between the actions. Each action will only perform block/unblock actions for
  /// this token. If there were other actions with different tokens that applied the block state, it will not be
  /// released!
  /// </remarks>
  public string BlockToken { get; private set; }

  /// <summary>Specifies an icon if status should be added to the building.</summary>
  /// <remarks>It can be <c>null</c> to indicate that status is not needed.</remarks>
  public string BlockIcon  { get; private set; }

  /// <summary>Local key for the status text.</summary>
  public string BlockText { get; private set;  }

  /// <summary>Specifies if the status icon should be shown above the building.</summary>
  public bool ShowIcon { get; private set;  }

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore UnusedMember.Global
  #endregion

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => ActionKind == ActionKindEnum.Block
      ? Behavior.Loc.T(BlockDescriptionLocKey)
      : Behavior.Loc.T(UnblockDescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new BlockableBuildingAction() {
        ActionKind = ActionKind,
        BlockToken = BlockToken,
        BlockIcon = BlockIcon,
        BlockText = BlockText,
        ShowIcon = ShowIcon,
    };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return behavior.GetComponentFast<BlockableBuilding>();
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    UpdateBlockableBuildingState();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    UpdateBlockableBuildingState();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    Blocker.UnblockBuilding();  // It cannot be undone by the user.
  }

  #endregion

  #region Implementation

  /// <summary>Returns a component that controls the blocking logic.</summary>
  /// <remarks>This component is dynamically created at the <see cref="Behaviour"/> when needed.</remarks>
  BuildingBlocker Blocker {
    get {
      if (!_blocker) {
        _blocker = GetBlocker();
      }
      return _blocker;
    }
  }
  BuildingBlocker _blocker;

  /// <summary>Returns existing blocker component or creates a new one.</summary>
  /// <remarks>
  /// On every behavior, there must be exactly one component per <see cref="BlockToken"/>. The blocker component is
  /// intentionally not created via Bindito. It would pollute the game objects with a component that most of them will
  /// never use. Thus, we create it dynamically only when needed.
  /// </remarks>
  BuildingBlocker GetBlocker() {
    var allBlockers = new List<BuildingBlocker>();
    Behavior.GetComponentsFast(allBlockers);
    var status = allBlockers.FirstOrDefault(x => x.BlockToken == BlockToken);
    if (!status) {
      var baseInstantiator = DependencyContainer.GetInstance<BaseInstantiator>();
      status = baseInstantiator.AddComponent<BuildingBlocker>(Behavior.GameObjectFast);
      status.SetBlockToken(BlockToken);
    }
    if (BlockIcon != null) {
      // The blocker could get created from the unblock action which doesn't have status setting.
      status.SetIcon(blockIcon: BlockIcon, description: Behavior.Loc.T(BlockText), withIcon: ShowIcon);
    }
    return status;
  }

  /// <summary>Ensures that the building's block state is in sync with the rule condition.</summary>
  void UpdateBlockableBuildingState() {
    if (!Condition.ConditionState) {
      return;
    }
    if (ActionKind == ActionKindEnum.Block) {
      Blocker.BlockBuilding();
    } else {
      Blocker.UnblockBuilding();
    }
  }

  #endregion

  #region IGameSerializable implemenationsa

  static readonly PropertyKey<string> ActionKindKey = new("ActionKind");
  static readonly PropertyKey<string> BlockTokenKey = new("BlockToken");
  static readonly PropertyKey<string> BlockIconKey = new("BlockIcon");
  static readonly PropertyKey<string> BlockTextKey = new("BlockText");
  static readonly PropertyKey<bool> ShowIconKey = new("ShowIcon");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    ActionKind = (ActionKindEnum)Enum.Parse(typeof(ActionKindEnum), objectLoader.Get(ActionKindKey), ignoreCase: false);
    BlockToken = objectLoader.Get(BlockTokenKey);
    if (ActionKind != ActionKindEnum.Block) {
      return;
    }
    BlockIcon = objectLoader.GetValueOrNull(BlockIconKey);
    BlockText = objectLoader.GetValueOrNull(BlockTextKey);
    ShowIcon = objectLoader.GetValueOrNullable(ShowIconKey) ?? false;
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ActionKindKey, ActionKind.ToString());
    objectSaver.Set(BlockTokenKey, BlockToken);
    if (BlockIcon == null) {
      return;
    }
    objectSaver.Set(BlockIconKey, BlockIcon);
    objectSaver.Set(BlockTextKey, BlockText);
    objectSaver.Set(ShowIconKey, ShowIcon);
  }

  #endregion

  #region Helper BaseComponent to show blocked status

  sealed class BuildingBlocker : BaseComponent {
    public string BlockToken { get; private set; }

    StatusToggle _statusToggle;
    BlockableBuilding _blockableBuilding;

    void Awake() {
      _blockableBuilding = GetComponentFast<BlockableBuilding>();
    }

    public void SetBlockToken(string statusTag) {
      BlockToken = statusTag;
    }

    public void SetIcon(string blockIcon, string description, bool withIcon) {
      if (blockIcon == null) {
        return;
      }
      _statusToggle = withIcon
          ? StatusToggle.CreateNormalStatusWithFloatingIcon(blockIcon, description)
          : StatusToggle.CreateNormalStatus(blockIcon, description);
      GetComponentFast<StatusSubject>().RegisterStatus(_statusToggle);
    }

    public void BlockBuilding() {
      _blockableBuilding.Block(BlockToken);
      _statusToggle?.Activate();
    }

    public void UnblockBuilding() {
      _blockableBuilding.Unblock(BlockToken);
      _statusToggle?.Deactivate();
    }
  }

  #endregion
}