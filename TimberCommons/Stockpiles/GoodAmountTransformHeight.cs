// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.InventorySystem;
using Timberborn.Workshops;
using UnityEngine;

namespace IgorZ.TimberCommons.Stockpiles;

/// <summary>
/// Replacement component for the stock <c>Timberborn.Stockpiles.GoodAmountTransformHeight</c>. It can work with
/// manufacture buildings as well as with simple stockpiles.
/// </summary>
public sealed class GoodAmountTransformHeight : BaseComponent, IAwakableComponent, IFinishedStateListener {

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    var manufactory = GetEnabledComponent<Manufactory>();
    _inventory = manufactory ? manufactory.Inventory : ComponentsAccessor.GetGoodsInventory(this);
    _inventory.InventoryChanged += OnInventoryChanged;
    foreach (var spec in _heightSpecs) {
      spec.MaxGoodAmount = _inventory.AllowedGoods.Single(
          goodAmount => goodAmount.StorableGood.GoodId == spec.GoodAmountSpec.Good).Amount;
      UpdateTargetHeight(spec);
    }
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {}

  #endregion

  #region Implementation

  record HeightSpec(GoodAmountTransformHeightSpec GoodAmountSpec, Transform Target) {
    public readonly float InitialHeight = Target.position.y;
    public int MaxGoodAmount;
  }

  readonly List<HeightSpec> _heightSpecs = [];
  Inventory _inventory;

  void AddHeightSpec(GoodAmountTransformHeightSpec spec) {
    _heightSpecs.Add(new HeightSpec(spec, GameObject.FindChildTransform(spec.TargetName)));
  }

  /// <inheritdoc/>
  public void Awake() {
    var singleSpec = GetComponent<GoodAmountTransformHeightSpec>();
    if (singleSpec != null) {
      AddHeightSpec(singleSpec);
    }
    var multiSpec = GetComponent<MultiGoodAmountTransformHeightSpec>();
    if (multiSpec != null) {
      foreach (var spec in multiSpec.GoodAmounts) {
        AddHeightSpec(spec);
      }
    }
  }
  
  void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
    var spec = _heightSpecs.FirstOrDefault(x => e.GoodId == x.GoodAmountSpec.Good);
    if (spec != null) {
      UpdateTargetHeight(spec);
    }
  }

  void UpdateTargetHeight(HeightSpec spec) {
    var num = Mathf.Clamp01((float)_inventory.AmountInStock(spec.GoodAmountSpec.Good) / spec.MaxGoodAmount);
    if (spec.GoodAmountSpec.NonLinearity != 0f) {
      num = (float)Math.Pow(num, spec.GoodAmountSpec.NonLinearity + 1f);
    }
    var localPosition = spec.Target.localPosition;
    localPosition.y = Mathf.Lerp(spec.InitialHeight, spec.GoodAmountSpec.MaxHeight, num);
    spec.Target.localPosition = localPosition;
  }

  #endregion
}
