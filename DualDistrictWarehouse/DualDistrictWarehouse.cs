using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.LinkedBuildingSystem;
using Timberborn.Stockpiles;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.DualDistrictWarehouse;

sealed class DualDistrictWarehouse : BaseComponent, IAwakableComponent, IFinishedStateListener {
  readonly MirrorOperationLock _mirrorOperationLock = new();
  readonly DualDistrictWarehouseRegistry _registry;

  Inventory _inventory;
  SingleGoodAllower _singleGoodAllower;
  DualDistrictWarehouse _linked;
  bool _registered;

  internal Inventory Inventory => _inventory;

  public DualDistrictWarehouse(DualDistrictWarehouseRegistry registry) {
    _registry = registry;
  }

  public void Awake() {
    _inventory = GetComponent<Stockpile>().Inventory;
    _singleGoodAllower = GetComponent<SingleGoodAllower>();
    GetComponent<LinkedBuilding>().BuildingLinked += OnBuildingLinked;
  }

  public void OnEnterFinishedState() {
    _inventory.InventoryEnabled += OnInventoryEnabled;
    _inventory.InventoryChanged += OnInventoryChanged;
    _inventory.InventoryStockChanged += OnInventoryStockChanged;
    _singleGoodAllower.DisallowedGoodsChanged += OnDisallowedGoodsChanged;
    TryInitializePair();
  }

  public void OnExitFinishedState() {
    _inventory.InventoryEnabled -= OnInventoryEnabled;
    _inventory.InventoryChanged -= OnInventoryChanged;
    _inventory.InventoryStockChanged -= OnInventoryStockChanged;
    _singleGoodAllower.DisallowedGoodsChanged -= OnDisallowedGoodsChanged;
    UnregisterPair();
  }

  void OnBuildingLinked(object sender, LinkedBuilding linkedBuilding) {
    _linked = linkedBuilding.GetComponent<DualDistrictWarehouse>();
    TryInitializePair();
  }

  void OnInventoryEnabled(object sender, EventArgs e) {
    TryInitializePair();
  }

  void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorStockReservation(e.GoodId, ReservedStock(e.GoodId));
    }
  }

  void OnInventoryStockChanged(object sender, InventoryStockChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorStock(e.GoodAmount);
    }
  }

  void OnDisallowedGoodsChanged(object sender, DisallowedGoodsChangedEventArgs e) {
    if (_mirrorOperationLock.IsUnlocked && IsOperationalPair()) {
      _linked.MirrorAllowedGood(_singleGoodAllower.AllowedGood);
    }
  }

  void MirrorStock(GoodAmount change) {
    using (_mirrorOperationLock.Lock()) {
      if (change.Amount > 0) {
        _inventory.GiveExistingIgnoringCapacityReservation(change);
      } else if (change.Amount < 0) {
        _inventory.TakeExisting(new GoodAmount(change.GoodId, -change.Amount));
      }
    }
  }

  void MirrorStockReservation(string goodId, int expectedReservation) {
    using (_mirrorOperationLock.Lock()) {
      var reservationDelta = expectedReservation - ReservedStock(goodId);
      if (reservationDelta > 0) {
        _inventory.ReserveStock(new GoodAmount(goodId, reservationDelta));
      } else if (reservationDelta < 0) {
        _inventory.UnreserveStock(new GoodAmount(goodId, -reservationDelta));
      }
    }
  }

  void MirrorAllowedGood(string goodId) {
    using (_mirrorOperationLock.Lock()) {
      _singleGoodAllower.Allow(goodId);
    }
  }

  int ReservedStock(string goodId) {
    return _inventory.AmountInStock(goodId) - _inventory.UnreservedAmountInStock(goodId);
  }

  void TryInitializePair() {
    if (!IsOperationalPair()) {
      return;
    }

    var primary = PrimaryHalf();
    var secondary = primary._linked;
    primary.VerifyReplicasMatch(secondary);
    if (!primary._registered) {
      primary._registry.Register(primary);
      primary._registered = true;
    }
  }

  void VerifyReplicasMatch(DualDistrictWarehouse other) {
    if (_singleGoodAllower.AllowedGood != other._singleGoodAllower.AllowedGood
        || !StockMatches(other) || !ReservationsMatch(other)) {
      HostedDebugLog.Error(this, "Linked warehouse inventories are out of sync. No automatic recovery was attempted.");
    }
  }

  bool StockMatches(DualDistrictWarehouse other) {
    var goods = new HashSet<string>();
    AddStockGoods(_inventory, goods);
    AddStockGoods(other._inventory, goods);
    foreach (var goodId in goods) {
      if (_inventory.AmountInStock(goodId) != other._inventory.AmountInStock(goodId)) {
        return false;
      }
    }
    return true;
  }

  bool ReservationsMatch(DualDistrictWarehouse other) {
    var goods = new HashSet<string>();
    AddStockGoods(_inventory, goods);
    AddStockGoods(other._inventory, goods);
    foreach (var goodId in goods) {
      if (ReservedStock(goodId) != other.ReservedStock(goodId)) {
        return false;
      }
    }
    return true;
  }

  static void AddStockGoods(Inventory inventory, HashSet<string> goods) {
    foreach (var good in inventory.Stock) {
      goods.Add(good.GoodId);
    }
  }

  void UnregisterPair() {
    var primary = PrimaryHalfOrNull();
    if (primary?._registered == true) {
      primary._registry.Unregister(primary);
      primary._registered = false;
    }
  }

  DualDistrictWarehouse PrimaryHalf() {
    return GetComponent<EntityComponent>().EntityId.CompareTo(_linked.GetComponent<EntityComponent>().EntityId) <= 0
        ? this
        : _linked;
  }

  DualDistrictWarehouse PrimaryHalfOrNull() {
    return _linked == null ? null : PrimaryHalf();
  }

  bool IsOperationalPair() {
    return _inventory.Enabled && _linked?._inventory.Enabled == true;
  }
}
