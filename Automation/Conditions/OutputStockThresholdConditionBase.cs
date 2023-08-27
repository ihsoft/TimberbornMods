// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using Automation.Core;
using Timberborn.InventorySystem;
using Timberborn.Persistence;

namespace Automation.Conditions {

/// <summary>Base class for conditions that check output goods in inventories for a specific threshold.</summary>
/// <remarks>This condition only triggers on the inventory changes.</remarks>
/// <seealso cref="Inventory"/>
[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
public abstract class OutputStockThresholdConditionBase : AutomationConditionBase {
  #region AutomationConditionBase implementation
  /// <inheritdoc/>
  public override void SyncState() {
    CheckInventory();
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    var inventory = behavior.GetComponentFast<Inventory>();
    return inventory != null && inventory.IsOutput && !inventory.IsInput && inventory.OutputGoods.Count == 1;
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    Inventory = Behavior.GetComponentFast<Inventory>();
    Inventory.InventoryStockChanged += OnInventoryAmountChangedEvent;
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Inventory.InventoryStockChanged -= OnInventoryAmountChangedEvent;
  }
  #endregion

  #region API
  /// <summary>Threshold in percents [0; 100].</summary>
  public int Threshold { get; protected set; }

  /// <summary>Inventory is only set when attached to an automation behavior.</summary>
  protected Inventory Inventory { get; private set; }

  /// <summary>A callback that verifies inventory state and sets the condition state.</summary>
  /// <remarks>It's only called if inventory stock has changed.</remarks>
  protected abstract void CheckInventory();
  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<int> ThresholdKey = new("Threshold");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    if (objectLoader.Has(ThresholdKey)) {
      Threshold = objectLoader.Get(ThresholdKey);
    }
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ThresholdKey, Threshold);
  }
  #endregion

  #region Implementation
  void OnInventoryAmountChangedEvent(object sender, InventoryAmountChangedEventArgs e) {
    CheckInventory();
  }
  #endregion
}

}
