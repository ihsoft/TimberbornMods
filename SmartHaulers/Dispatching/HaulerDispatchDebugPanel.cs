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
  readonly DispatchPerformanceStats _performanceStats;
  readonly UILayout _uiLayout;

  VisualElement _root;
  Label _titleLabel;
  Label _contentLabel;
  bool _isAddedToLayout;
  string _lastText;

  public HaulerDispatchDebugPanel(
      DispatchCenterRegistry dispatchCenterRegistry, DispatchPerformanceStats performanceStats, UILayout uiLayout) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _performanceStats = performanceStats;
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
    if (SmartHaulersState.DispatchViewMode == DispatchDebugViewMode.Perf) {
      AddPerformance(lines);
      return string.Join("\n", lines);
    }
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

  void AddPerformance(List<string> lines) {
    var last = _performanceStats.LastSample;
    var total = _performanceStats.WindowTotal;
    var sampleCount = _performanceStats.WindowSampleCount;
    if (sampleCount == 0) {
      lines.Add("No samples");
      return;
    }
    lines.Add($"window={sampleCount}");
    lines.Add($"last, {FormatSample(last)}");
    lines.Add($"avg,  {FormatAverage(total, sampleCount)}");
    lines.Add($"last phases, {FormatBreakdown(last)}");
    lines.Add($"avg phases,  {FormatAverageBreakdown(total, sampleCount)}");
    lines.Add("T total, PU pickup path, DL delivery path");
    lines.Add("A agents, O active orders, Q queued orders, B construction, R readiness, D decisions, S sort, X other");
    lines.Add("PU/DL are included in phase times.");
  }

  static string FormatSample(DispatchPerformanceSample sample) {
    return $"T={Milliseconds(sample.TotalTicks):0.###}ms, "
        + $"PU={Milliseconds(sample.PickupPathTicks):0.###}ms/{sample.PickupPathCalls}, "
        + $"DL={Milliseconds(sample.DeliveryPathTicks):0.###}ms/{sample.DeliveryPathCalls}";
  }

  static string FormatBreakdown(DispatchPerformanceSample sample) {
    return $"A={Milliseconds(sample.AgentTicks):0.###}, "
        + $"O={Milliseconds(sample.ActiveOrderTicks):0.###}, "
        + $"Q={Milliseconds(sample.QueuedOrderTicks):0.###}, "
        + $"B={Milliseconds(sample.ConstructionOrderTicks):0.###}, "
        + $"R={Milliseconds(sample.ReadinessTicks):0.###}, "
        + $"D={Milliseconds(sample.DecisionTicks):0.###}, "
        + $"S={Milliseconds(sample.SortTicks):0.###}, "
        + $"X={Milliseconds(OtherTicks(sample)):0.###}ms";
  }

  static string FormatAverage(DispatchPerformanceSample total, int sampleCount) {
    return $"T={Milliseconds(total.TotalTicks) / sampleCount:0.###}ms, "
        + $"PU={Milliseconds(total.PickupPathTicks) / sampleCount:0.###}ms/"
        + $"{(float)total.PickupPathCalls / sampleCount:0.#}, "
        + $"DL={Milliseconds(total.DeliveryPathTicks) / sampleCount:0.###}ms/"
        + $"{(float)total.DeliveryPathCalls / sampleCount:0.#}";
  }

  static string FormatAverageBreakdown(DispatchPerformanceSample total, int sampleCount) {
    return $"A={Milliseconds(total.AgentTicks) / sampleCount:0.###}, "
        + $"O={Milliseconds(total.ActiveOrderTicks) / sampleCount:0.###}, "
        + $"Q={Milliseconds(total.QueuedOrderTicks) / sampleCount:0.###}, "
        + $"B={Milliseconds(total.ConstructionOrderTicks) / sampleCount:0.###}, "
        + $"R={Milliseconds(total.ReadinessTicks) / sampleCount:0.###}, "
        + $"D={Milliseconds(total.DecisionTicks) / sampleCount:0.###}, "
        + $"S={Milliseconds(total.SortTicks) / sampleCount:0.###}, "
        + $"X={Milliseconds(OtherTicks(total)) / sampleCount:0.###}ms";
  }

  static long OtherTicks(DispatchPerformanceSample sample) {
    var knownTicks = sample.AgentTicks
        + sample.ActiveOrderTicks
        + sample.QueuedOrderTicks
        + sample.ConstructionOrderTicks
        + sample.ReadinessTicks
        + sample.DecisionTicks
        + sample.SortTicks;
    return sample.TotalTicks > knownTicks ? sample.TotalTicks - knownTicks : 0;
  }

  static double Milliseconds(long ticks) {
    return DispatchPerformanceStats.TicksToMilliseconds(ticks);
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
    if (viewMode is DispatchDebugViewMode.Agents) {
      foreach (var agent in dispatchCenter.Agents.OrderBy(agent => agent.EntityId)) {
        lines.Add(FormatAgent(agent));
      }
    }
    if (viewMode is DispatchDebugViewMode.Orders) {
      foreach (var order in dispatchCenter.Orders) {
        lines.Add(FormatOrder(order));
      }
    }
  }

  static (
      int available, int wandering, int workplaceIdle, int transporting, int satisfyingNeed, int working) CountAgents(
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
    return $"  {agent.DisplayName}, {TransportDebugFormatter.FormatAgentState(agent)}, {agent.Activity}, "
        + $"{agent.Position}, {agent.Speed:0.##}, {agent.Capacity}";
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
        "SmartHaulers snapshot columns: perf mode: T/PU/DL and phase timings | agents/orders modes: view, district, "
        + "agents, available, wandering, workplaceIdle, transporting, satisfyingNeed, working, orders | agent, "
        + "state/role, activity, position, speed, capacity | agent, phase, good, path, route, left | phase(weight), "
        + "behavior, optional good, optional path, decision, requester");
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
    return phase is OrderPhase.Queued or OrderPhase.Estimated or OrderPhase.Deferred or OrderPhase.Dispatchable
        or OrderPhase.Covered;
  }
}
