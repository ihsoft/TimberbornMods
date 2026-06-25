// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.SmartHaulers.Core;
using IgorZ.SmartHaulers.Dispatching;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.WorkSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartHaulers.DispatchingUI;

sealed class TransportAgentFragment : IEntityPanelFragment {
  readonly DispatchCenterRegistry _dispatchCenterRegistry;
  readonly TransportDebugRowFactory _rowFactory;

  VisualElement _root;
  VisualElement _agentRow;
  VisualElement _orderRow;
  Worker _selectedWorker;
  string _lastAgentText;
  string _lastOrderText;

  public TransportAgentFragment(DispatchCenterRegistry dispatchCenterRegistry, TransportDebugRowFactory rowFactory) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _rowFactory = rowFactory;
  }

  public VisualElement InitializeFragment() {
    _root = CreateRoot();
    _agentRow = new VisualElement();
    _orderRow = new VisualElement();
    _root.Add(_agentRow);
    _root.Add(_orderRow);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _selectedWorker = entity.GetComponent<Worker>();
    UpdateContent();
  }

  public void ClearFragment() {
    _selectedWorker = null;
    _lastAgentText = null;
    _lastOrderText = null;
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
    UpdateContent();
  }

  void UpdateContent() {
    if (_root == null || !SmartHaulersState.DiagnosticsEnabled || !_selectedWorker) {
      _root?.ToggleDisplayStyle(visible: false);
      return;
    }
    if (!TryFindAgent(_selectedWorker, out var agent)) {
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    var agentText = TransportDebugFormatter.FormatAgentVerbose(agent);
    if (agentText != _lastAgentText) {
      _lastAgentText = agentText;
      _agentRow.Clear();
      _agentRow.Add(_rowFactory.CreateAgentRow(agent));
    }
    var orderText = TryFindOrder(agent, out var order)
        ? TransportDebugFormatter.FormatOrderVerbose(order)
        : "order=none";
    if (orderText != _lastOrderText) {
      _lastOrderText = orderText;
      _orderRow.Clear();
      _orderRow.Add(orderText != "order=none"
          ? _rowFactory.CreateOrderRow(order, includeAgent: false)
          : CreateLabel(orderText));
    }
    _root.ToggleDisplayStyle(visible: true);
  }

  bool TryFindAgent(Worker worker, out TransportAgentSnapshot agent) {
    foreach (var dispatchCenter in _dispatchCenterRegistry.DispatchCenters) {
      foreach (var candidate in dispatchCenter.Agents) {
        if (candidate.Worker == worker) {
          agent = candidate;
          return true;
        }
      }
    }
    agent = default;
    return false;
  }

  bool TryFindOrder(TransportAgentSnapshot agent, out TransportOrderSnapshot order) {
    foreach (var dispatchCenter in _dispatchCenterRegistry.DispatchCenters) {
      var match = dispatchCenter.Orders.FirstOrDefault(candidate => candidate.AgentId == agent.EntityId);
      if (match.Worker) {
        order = match;
        return true;
      }
    }
    order = default;
    return false;
  }

  static VisualElement CreateRoot() {
    var root = new VisualElement {
        name = "SmartHaulersTransportAgentFragment",
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

  static Label CreateLabel(string text) {
    var label = new Label(text);
    label.style.whiteSpace = WhiteSpace.Normal;
    label.style.color = Color.white;
    return label;
  }
}
