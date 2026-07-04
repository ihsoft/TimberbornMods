// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.SmartHaulers.Core;
using IgorZ.SmartHaulers.Dispatching;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartHaulers.DispatchingUI;

sealed class TransportRequesterFragment(
    DispatchCenterRegistry dispatchCenterRegistry,
    TransportDebugRowFactory rowFactory) : IEntityPanelFragment {
  readonly List<TransportOrderSnapshot> _orders = [];

  VisualElement _root;
  Label _titleLabel;
  VisualElement _ordersContainer;
  BaseComponent _selectedEntity;
  Guid _selectedEntityId;
  string _lastOrdersText;

  public VisualElement InitializeFragment() {
    _root = CreateRoot();
    _titleLabel = CreateLabel(FontStyle.Bold);
    _ordersContainer = new VisualElement();
    _root.Add(_titleLabel);
    _root.Add(_ordersContainer);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _selectedEntity = entity;
    _selectedEntityId = entity.GetComponent<EntityComponent>()?.EntityId ?? Guid.Empty;
    UpdateContent();
  }

  public void ClearFragment() {
    _selectedEntity = null;
    _selectedEntityId = Guid.Empty;
    _lastOrdersText = null;
    _orders.Clear();
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
    UpdateContent();
  }

  void UpdateContent() {
    if (_root == null || !SmartHaulersState.DiagnosticsEnabled || !_selectedEntity || _selectedEntityId == Guid.Empty) {
      _root?.ToggleDisplayStyle(visible: false);
      return;
    }
    FindOrders();
    if (_orders.Count == 0) {
      _lastOrdersText = null;
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    _titleLabel.text = $"SmartHaulers orders: {_orders.Count}";
    var ordersText = FormatOrdersSignature();
    if (ordersText != _lastOrdersText) {
      _lastOrdersText = ordersText;
      UpdateOrders();
    }
    _root.ToggleDisplayStyle(visible: true);
  }

  void FindOrders() {
    _orders.Clear();
    foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
      foreach (var order in dispatchCenter.Orders) {
        if (BelongsToSelectedEntity(order)) {
          _orders.Add(order);
        }
      }
    }
    _orders.Sort(CompareOrders);
  }

  bool BelongsToSelectedEntity(TransportOrderSnapshot order) {
    if (order.RequesterId == _selectedEntityId) {
      return true;
    }
    return InventoryBelongsToSelectedEntity(order.Source) || InventoryBelongsToSelectedEntity(order.Target);
  }

  bool InventoryBelongsToSelectedEntity(Inventory inventory) {
    if (!inventory) {
      return false;
    }
    if (inventory.GetComponent<EntityComponent>()?.EntityId == _selectedEntityId) {
      return true;
    }
    var selectedInventories = new List<Inventory>();
    _selectedEntity.GetComponents(selectedInventories);
    foreach (var selectedInventory in selectedInventories) {
      if (selectedInventory == inventory) {
        return true;
      }
    }
    var inventories = _selectedEntity.GetComponent<Inventories>();
    if (!inventories) {
      return false;
    }
    foreach (var selectedInventory in inventories.AllInventories) {
      if (selectedInventory == inventory) {
        return true;
      }
    }
    return false;
  }

  void UpdateOrders() {
    _ordersContainer.Clear();
    _ordersContainer.Add(CreateLabel(TransportDebugFormatter.FormatObject(_selectedEntity), FontStyle.Normal));
    for (var i = 0; i < _orders.Count; i++) {
      if (_orders.Count > 1 && i > 0) {
        _ordersContainer.Add(CreateSpacer());
      }
      _ordersContainer.Add(rowFactory.CreateOrderRow(_orders[i], includeRequester: false));
    }
  }

  string FormatOrdersSignature() {
    var lines = new List<string> {
        TransportDebugFormatter.FormatObject(_selectedEntity),
    };
    foreach (var order in _orders) {
      lines.Add(TransportDebugFormatter.FormatOrderVerbose(order, includeRequester: false));
    }
    return string.Join("\n", lines);
  }

  static int CompareOrders(TransportOrderSnapshot left, TransportOrderSnapshot right) {
    var phaseComparison = left.Phase.CompareTo(right.Phase);
    if (phaseComparison != 0) {
      return phaseComparison;
    }
    var behaviorComparison = string.CompareOrdinal(left.BehaviorName, right.BehaviorName);
    if (behaviorComparison != 0) {
      return behaviorComparison;
    }
    var weightComparison = right.Weight.CompareTo(left.Weight);
    if (weightComparison != 0) {
      return weightComparison;
    }
    var goodComparison = string.CompareOrdinal(left.Cargo.GoodId, right.Cargo.GoodId);
    if (goodComparison != 0) {
      return goodComparison;
    }
    return left.Cargo.Amount.CompareTo(right.Cargo.Amount);
  }

  static VisualElement CreateRoot() {
    var root = new VisualElement {
        name = "SmartHaulersTransportRequesterFragment",
    };
    root.style.width = 450;
    root.style.minWidth = 450;
    root.style.alignSelf = Align.FlexEnd;
    root.style.marginTop = 4;
    root.style.marginBottom = 4;
    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 6;
    root.style.paddingBottom = 6;
    root.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
    return root;
  }

  static Label CreateLabel(FontStyle fontStyle) {
    var label = new Label();
    label.style.whiteSpace = WhiteSpace.Normal;
    label.style.color = Color.white;
    label.style.unityFontStyleAndWeight = fontStyle;
    return label;
  }

  static Label CreateLabel(string text, FontStyle fontStyle) {
    var label = CreateLabel(fontStyle);
    label.text = text;
    return label;
  }

  static VisualElement CreateSpacer() {
    var spacer = new VisualElement();
    spacer.style.height = 6;
    return spacer;
  }
}
