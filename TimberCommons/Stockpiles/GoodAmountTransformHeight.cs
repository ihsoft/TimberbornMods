// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.InventorySystem;
using Timberborn.Workshops;
using UnityEngine;

namespace IgorZ.TimberCommons.Stockpiles;

/// <summary>
/// Replacement component for the stock <c>Timberborn.Stockpiles.GoodAmountTransformHeight</c>. It can work with
/// manufacture buildings as well as with simple stockiples.
/// </summary>
public sealed class GoodAmountTransformHeight : BaseComponent, IFinishedStateListener {
  
  #region Fields for Unity
  // ReSharper disable InconsistentNaming

  [SerializeField]
  public string _targetName;

  [SerializeField]
  public float _maxHeight;

  [SerializeField]
  public string _good;

  [SerializeField]
  public float _nonLinearity;

  // ReSharper restore InconsistentNaming
  #endregion

  #region IFinishedStateListener implementation

  public void OnEnterFinishedState() {
    Inventory.InventoryChanged += OnInventoryChanged;
    _maxGoodAmount = Inventory.AllowedGoods.Single(goodAmount => goodAmount.StorableGood.GoodId == _good).Amount;
    UpdateTargetHeight();
  }

  public void OnExitFinishedState() {
    Inventory.InventoryChanged -= OnInventoryChanged;
  }

  #endregion

  #region Implementation

  Transform _target;
  float _initialHeight;
  int _maxGoodAmount;

  Manufactory _manufactory;
  Inventory Inventory => _inventory ??= _manufactory ? _manufactory.Inventory : GetEnabledComponent<Inventory>();
  Inventory _inventory;

  void Awake() {
    _target = GameObjectFast.FindChildTransform(_targetName);
    _initialHeight = _target.position.y;
    _manufactory = GetEnabledComponent<Manufactory>();
  }
  
  void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
    if (e.GoodId != _good) {
      return;
    }
    UpdateTargetHeight();
  }

  void UpdateTargetHeight() {
    var num = Mathf.Clamp01((float)Inventory.AmountInStock(_good) / _maxGoodAmount);
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
