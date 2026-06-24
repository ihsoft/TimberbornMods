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

sealed class TransportAgentFragment(DispatchCenterRegistry dispatchCenterRegistry) : IEntityPanelFragment {
  VisualElement _root;
  Label _agentLabel;
  Label _orderLabel;
  Worker _selectedWorker;

  public VisualElement InitializeFragment() {
    _root = CreateRoot();
    _agentLabel = CreateLabel(FontStyle.Bold);
    _orderLabel = CreateLabel(FontStyle.Normal);
    _root.Add(_agentLabel);
    _root.Add(_orderLabel);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  public void ShowFragment(BaseComponent entity) {
    _selectedWorker = entity.GetComponent<Worker>();
    UpdateContent();
  }

  public void ClearFragment() {
    _selectedWorker = null;
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
    _agentLabel.text = TransportDebugFormatter.FormatAgentVerbose(agent);
    _orderLabel.text = TryFindOrder(agent, out var order)
        ? TransportDebugFormatter.FormatOrderVerbose(order)
        : "order=none";
    _root.ToggleDisplayStyle(visible: true);
  }

  bool TryFindAgent(Worker worker, out TransportAgentSnapshot agent) {
    foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
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
    foreach (var dispatchCenter in dispatchCenterRegistry.DispatchCenters) {
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

  static Label CreateLabel(FontStyle fontStyle) {
    var label = new Label();
    label.style.whiteSpace = WhiteSpace.Normal;
    label.style.color = Color.white;
    label.style.unityFontStyleAndWeight = fontStyle;
    return label;
  }
}
