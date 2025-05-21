// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
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
public sealed class GoodAmountTransformHeight : BaseComponent, IFinishedStateListener {
  
  #region Fields for Unity
  // ReSharper disable InconsistentNaming

  [SerializeField]
  string _targetName= "";

  [SerializeField]
  float _maxHeight = 0f;

  [SerializeField]
  string _good = "";

  [SerializeField]
  float _nonLinearity = 0f;

  // ReSharper restore InconsistentNaming
  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    var manufactory = GetEnabledComponent<Manufactory>();
    _inventory = manufactory ? manufactory.Inventory : ComponentsAccessor.GetInventory(this);
    _inventory.InventoryChanged += OnInventoryChanged;
    _maxGoodAmount = _inventory.AllowedGoods.Single(goodAmount => goodAmount.StorableGood.GoodId == _good).Amount;
    UpdateTargetHeight();
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    _inventory.InventoryChanged -= OnInventoryChanged;
  }

  #endregion

  #region Implementation

  Transform _target;
  float _initialHeight;
  int _maxGoodAmount;

  Inventory _inventory;

  void Awake() {
    _target = GameObjectFast.FindChildTransform(_targetName);
    _initialHeight = _target.position.y;
  }
  
  void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
    if (e.GoodId != _good) {
      return;
    }
    UpdateTargetHeight();
  }

  void UpdateTargetHeight() {
    var num = Mathf.Clamp01((float)_inventory.AmountInStock(_good) / _maxGoodAmount);
    if (_nonLinearity != 0f) {
      num = (float)Math.Pow(num, _nonLinearity + 1f);
    }
    var targetHeight = Mathf.Lerp(_initialHeight, _maxHeight, num);
    SetTargetHeight(targetHeight);
  }

  void SetTargetHeight(float height) {
    var localPosition = _target.localPosition;
    localPosition.y = height;
    _target.localPosition = localPosition;
  }

  #endregion
}
