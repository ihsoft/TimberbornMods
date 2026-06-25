// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.SmartHaulers.Core;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulerDispatchDebugPanel : IPostLoadableSingleton, IUpdatableSingleton {
  readonly DispatchCenterRegistry _dispatchCenterRegistry;
  readonly UILayout _uiLayout;

  VisualElement _root;
  Label _titleLabel;
  Label _contentLabel;
  bool _isAddedToLayout;
  string _lastText;

  public HaulerDispatchDebugPanel(DispatchCenterRegistry dispatchCenterRegistry, UILayout uiLayout) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _uiLayout = uiLayout;
  }

  public void PostLoad() {
    _root = CreateRoot();
    _titleLabel = CreateTitleLabel();
    _contentLabel = CreateContentLabel();
    var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
    scrollView.contentContainer.Add(_contentLabel);
    _root.Add(_titleLabel);
    _root.Add(scrollView);
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateSingleton() {
    if (!_isAddedToLayout) {
      _uiLayout.AddAbsoluteItem(_root);
      _isAddedToLayout = true;
    }
    var visible = SmartHaulersState.DiagnosticsEnabled && SmartHaulersState.DispatchPanelVisible;
    _root.ToggleDisplayStyle(visible);
    if (!visible) {
      return;
    }
    if (SmartHaulersState.ConsumeSnapshotRefreshRequest()) {
      RefreshSnapshots();
    }
    _titleLabel.text = $"SmartHaulers dispatch: {SmartHaulersState.DispatchViewMode}";
    var text = BuildText();
    if (SmartHaulersState.ConsumeLogSnapshotRequest()) {
      LogSnapshot(text);
    }
    if (text == _lastText) {
      return;
    }
    _lastText = text;
    _contentLabel.text = text;
  }

  static VisualElement CreateRoot() {
    var root = new VisualElement {
        name = "SmartHaulersDebugPanel",
    };
    root.style.position = Position.Absolute;
    root.style.left = 8;
    root.style.top = new StyleLength(new Length(38, LengthUnit.Percent));
    root.style.width = 760;
    root.style.maxHeight = new StyleLength(new Length(55, LengthUnit.Percent));
    root.style.paddingLeft = 8;
    root.style.paddingRight = 8;
    root.style.paddingTop = 6;
    root.style.paddingBottom = 6;
    root.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
    root.style.borderTopLeftRadius = 3;
    root.style.borderTopRightRadius = 3;
    root.style.borderBottomLeftRadius = 3;
    root.style.borderBottomRightRadius = 3;
    return root;
  }

  static Label CreateTitleLabel() {
    var label = new Label();
    label.style.unityFontStyleAndWeight = FontStyle.Bold;
    label.style.color = Color.white;
    label.style.marginBottom = 4;
    return label;
  }

  static Label CreateContentLabel() {
    var label = new Label();
    label.style.color = Color.white;
    label.style.whiteSpace = WhiteSpace.NoWrap;
    return label;
  }

  string BuildText() {
    var lines = new List<string>();
    foreach (var dispatchCenter in OrderedDispatchCenters()) {
      AddDispatchCenter(lines, dispatchCenter);
    }
    return lines.Count > 0 ? string.Join("\n", lines) : "No districts";
  }

  void RefreshSnapshots() {
    foreach (var dispatchCenter in _dispatchCenterRegistry.DispatchCenters) {
      dispatchCenter.RefreshSnapshot();
    }
  }

  IEnumerable<HaulerDispatchCenter> OrderedDispatchCenters() {
    return _dispatchCenterRegistry.DispatchCenters
        .Where(dispatchCenter => dispatchCenter.DistrictCenter && dispatchCenter.Agents.Count > 0)
        .OrderBy(dispatchCenter => dispatchCenter.DistrictCenterId);
  }

  static void AddDispatchCenter(List<string> lines, HaulerDispatchCenter dispatchCenter) {
    var counts = CountAgents(dispatchCenter.Agents);
    var viewMode = SmartHaulersState.DispatchViewMode;
    lines.Add(
        $"{viewMode}, {TransportDebugFormatter.FormatObject(dispatchCenter.DistrictCenter)}, "
        + $"{dispatchCenter.Agents.Count}, "
        + $"{counts.available}, {counts.wandering}, {counts.workplaceIdle}, {counts.transporting}, "
        + $"{counts.satisfyingNeed}, {counts.working}, {dispatchCenter.Orders.Count}");
    if (viewMode is DispatchDebugViewMode.All or DispatchDebugViewMode.Agents) {
      foreach (var agent in dispatchCenter.Agents.OrderBy(agent => agent.EntityId)) {
        lines.Add(FormatAgent(agent));
      }
    }
    if (viewMode is DispatchDebugViewMode.All or DispatchDebugViewMode.Orders) {
      foreach (var order in dispatchCenter.Orders) {
        lines.Add(FormatOrder(order));
      }
    }
  }

  static (int available, int wandering, int workplaceIdle, int transporting, int satisfyingNeed, int working) CountAgents(
      IReadOnlyList<TransportAgentSnapshot> agents) {
    var available = 0;
    var wandering = 0;
    var workplaceIdle = 0;
    var transporting = 0;
    var satisfyingNeed = 0;
    var working = 0;
    foreach (var agent in agents) {
      switch (agent.State) {
        case TransportAgentState.Available:
          available++;
          break;
        case TransportAgentState.IdleWandering:
          wandering++;
          break;
        case TransportAgentState.WorkplaceIdle:
          workplaceIdle++;
          break;
        case TransportAgentState.Transporting:
          transporting++;
          break;
        case TransportAgentState.SatisfyingNeed:
          satisfyingNeed++;
          break;
        case TransportAgentState.Working:
          working++;
          break;
      }
    }
    return (available, wandering, workplaceIdle, transporting, satisfyingNeed, working);
  }

  static string FormatAgent(TransportAgentSnapshot agent) {
    return $"  {agent.DisplayName}, {agent.State}, {agent.Activity}, {agent.Position}, {agent.Speed:0.##}, "
        + $"{agent.Capacity}";
  }

  static string FormatOrder(TransportOrderSnapshot order) {
    if (IsUnassignedOrder(order.Phase)) {
      return $"  {FormatUnassignedOrder(order)}, "
          + $"{TransportDebugFormatter.FormatObject(order.Requester)}";
    }
    return $"  {TransportAgentSnapshot.FormatWorker(order.Worker)}, {order.Phase}, {order.GoodAmount}, "
        + $"{TransportDebugFormatter.FormatRoute(order)}, "
        + $"{order.RouteDistance:0.##}, {order.RemainingDistance:0.##}";
  }

  static void LogSnapshot(string text) {
    DebugEx.Info(
        "SmartHaulers snapshot columns: view, district, agents, available, wandering, workplaceIdle, transporting, "
        + "satisfyingNeed, working, orders | agent, state, activity, position, speed, capacity | agent, phase, "
        + "good, path, route, left | phase(weight), behavior, optional good, optional path, decision, requester");
    DebugEx.Info("SmartHaulers snapshot:\n{0}", text);
  }

  static string FormatUnassignedOrder(TransportOrderSnapshot order) {
    var text = $"{TransportDebugFormatter.FormatPhase(order)}, {order.BehaviorName}";
    text = AppendPart(text, TransportDebugFormatter.FormatCargo(order));
    text = AppendPart(text, TransportDebugFormatter.FormatKnownRoute(order));
    return text + TransportDebugFormatter.FormatDecision(order.Decision);
  }

  static string AppendPart(string text, string part) {
    return string.IsNullOrEmpty(part) ? text : $"{text}, {part}";
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Covered or OrderPhase.Estimated;
  }
}
