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
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartHaulers.DispatchingUI;

sealed class TransportRequesterFragment(DispatchCenterRegistry dispatchCenterRegistry) : IEntityPanelFragment {
  readonly List<TransportOrderSnapshot> _orders = [];

  VisualElement _root;
  Label _titleLabel;
  Label _ordersLabel;
  BaseComponent _selectedEntity;
  Guid _selectedEntityId;

  public VisualElement InitializeFragment() {
    _root = CreateRoot();
    _titleLabel = CreateLabel(FontStyle.Bold);
    _ordersLabel = CreateLabel(FontStyle.Normal);
    _root.Add(_titleLabel);
    _root.Add(_ordersLabel);
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
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    _titleLabel.text = $"SmartHaulers orders: {_orders.Count}";
    _ordersLabel.text = FormatOrders();
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
    return inventory && inventory.GetComponent<EntityComponent>()?.EntityId == _selectedEntityId;
  }

  string FormatOrders() {
    var lines = new List<string> {
        $"{DebugEx.ObjectToString(_selectedEntity)}",
    };
    for (var i = 0; i < _orders.Count; i++) {
      if (_orders.Count > 1 && i > 0) {
        lines.Add("");
      }
      lines.Add(TransportDebugFormatter.FormatOrderVerbose(_orders[i], includeRequester: false));
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
    return right.Weight.CompareTo(left.Weight);
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
}
