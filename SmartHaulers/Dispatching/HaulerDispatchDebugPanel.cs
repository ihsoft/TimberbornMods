// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.SmartHaulers.Core;
using IgorZ.SmartHaulers.DispatchingUI;
using IgorZ.TimberDev.UI;
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
  readonly HaulerDispatchRefreshService _refreshService;
  readonly TransportDebugRowFactory _rowFactory;
  readonly UiFactory _uiFactory;
  readonly UILayout _uiLayout;

  VisualElement _root;
  Label _titleLabel;
  ResizableDropdownElement _filterDropdown;
  VisualElement _contentContainer;
  bool _isAddedToLayout;
  bool _updatingFilterDropdown;
  DispatchDebugViewMode? _filterDropdownMode;
  string _lastText;

  public HaulerDispatchDebugPanel(
      DispatchCenterRegistry dispatchCenterRegistry,
      DispatchPerformanceStats performanceStats,
      HaulerDispatchRefreshService refreshService,
      TransportDebugRowFactory rowFactory,
      UiFactory uiFactory,
      UILayout uiLayout) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _performanceStats = performanceStats;
    _refreshService = refreshService;
    _rowFactory = rowFactory;
    _uiFactory = uiFactory;
    _uiLayout = uiLayout;
  }

  public void PostLoad() {
    _root = CreateRoot();
    _titleLabel = CreateTitleLabel();
    _filterDropdown = CreateFilterDropdown();
    _contentContainer = new VisualElement();
    var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
    scrollView.contentContainer.Add(_contentContainer);
    _root.Add(_titleLabel);
    _root.Add(_filterDropdown);
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
    UpdateFilterDropdown();
    _titleLabel.text = $"SmartHaulers dispatch: {FormatViewTitle()}";
    var text = BuildText();
    if (SmartHaulersState.ConsumeLogSnapshotRequest()) {
      LogSnapshot(text);
    }
    if (text == _lastText) {
      return;
    }
    _lastText = text;
    UpdateContent();
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

  static Label CreateContentLabel(string text) {
    var label = new Label(text);
    label.style.whiteSpace = WhiteSpace.Normal;
    label.style.color = Color.white;
    return label;
  }

  ResizableDropdownElement CreateFilterDropdown() {
    var dropdown = _uiFactory.CreateSimpleDropdown(OnFilterSelected);
    dropdown.AutoResizeToOptions = false;
    dropdown.style.width = 170;
    dropdown.style.marginBottom = 4;
    return dropdown;
  }

  void UpdateFilterDropdown() {
    var viewMode = SmartHaulersState.DispatchViewMode;
    var visible = viewMode is DispatchDebugViewMode.Agents or DispatchDebugViewMode.Orders;
    _filterDropdown.ToggleDisplayStyle(visible);
    if (!visible) {
      _filterDropdownMode = null;
      return;
    }
    if (_filterDropdownMode != viewMode) {
      _updatingFilterDropdown = true;
      _filterDropdown.Items = viewMode == DispatchDebugViewMode.Agents
          ? FilterItems<DispatchAgentFilter>()
          : FilterItems<DispatchOrderFilter>();
      _filterDropdown.SelectedValue = CurrentFilterValue();
      _updatingFilterDropdown = false;
      _filterDropdownMode = viewMode;
      return;
    }
    if (_filterDropdown.SelectedValue != CurrentFilterValue()) {
      _updatingFilterDropdown = true;
      _filterDropdown.SelectedValue = CurrentFilterValue();
      _updatingFilterDropdown = false;
    }
  }

  void OnFilterSelected(string value) {
    if (_updatingFilterDropdown) {
      return;
    }
    if (SmartHaulersState.DispatchViewMode == DispatchDebugViewMode.Agents
        && System.Enum.TryParse<DispatchAgentFilter>(value, out var agentFilter)) {
      SmartHaulersState.SetAgentFilter(agentFilter);
    }
    if (SmartHaulersState.DispatchViewMode == DispatchDebugViewMode.Orders
        && System.Enum.TryParse<DispatchOrderFilter>(value, out var orderFilter)) {
      SmartHaulersState.SetOrderFilter(orderFilter);
    }
  }

  static DropdownItem[] FilterItems<T>() where T : struct, System.Enum {
    return System.Enum.GetValues(typeof(T))
        .Cast<T>()
        .Select(value => new DropdownItem { Value = value.ToString(), Text = value.ToString() })
        .ToArray();
  }

  static string CurrentFilterValue() {
    return SmartHaulersState.DispatchViewMode switch {
        DispatchDebugViewMode.Agents => SmartHaulersState.AgentFilter.ToString(),
        DispatchDebugViewMode.Orders => SmartHaulersState.OrderFilter.ToString(),
        _ => "",
    };
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
    _refreshService.RefreshSnapshots();
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

  void UpdateContent() {
    _contentContainer.Clear();
    if (SmartHaulersState.DispatchViewMode == DispatchDebugViewMode.Perf) {
      AddPerformanceLabels(_contentContainer);
      return;
    }
    foreach (var dispatchCenter in OrderedDispatchCenters()) {
      AddDispatchCenterContent(_contentContainer, dispatchCenter);
    }
    if (_contentContainer.childCount == 0) {
      _contentContainer.Add(CreateContentLabel("No districts"));
    }
  }

  void AddPerformanceLabels(VisualElement content) {
    var lines = new List<string>();
    AddPerformance(lines);
    foreach (var line in lines) {
      content.Add(CreateContentLabel(line));
    }
  }

  void AddDispatchCenterContent(VisualElement content, HaulerDispatchCenter dispatchCenter) {
    var counts = CountAgents(dispatchCenter.Agents);
    content.Add(CreateContentLabel(
        $"{FormatViewTitle()}, {TransportDebugFormatter.FormatObject(dispatchCenter.DistrictCenter)}, "
        + $"{dispatchCenter.Agents.Count}, "
        + $"{counts.available}, {counts.wandering}, {counts.workplaceIdle}, {counts.transporting}, "
        + $"{counts.satisfyingNeed}, {counts.working}, {dispatchCenter.Orders.Count}"));
    if (SmartHaulersState.DispatchViewMode is DispatchDebugViewMode.Agents) {
      foreach (var agent in dispatchCenter.Agents.Where(MatchesAgentFilter).OrderBy(agent => agent.EntityId)) {
        content.Add(_rowFactory.CreateAgentRow(agent));
      }
    }
    if (SmartHaulersState.DispatchViewMode is DispatchDebugViewMode.Orders) {
      foreach (var order in OrdersForDisplay(dispatchCenter.Orders)) {
        content.Add(_rowFactory.CreateOrderRow(order));
      }
    }
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
    lines.Add(
        $"{FormatViewTitle()}, {TransportDebugFormatter.FormatObject(dispatchCenter.DistrictCenter)}, "
        + $"{dispatchCenter.Agents.Count}, "
        + $"{counts.available}, {counts.wandering}, {counts.workplaceIdle}, {counts.transporting}, "
        + $"{counts.satisfyingNeed}, {counts.working}, {dispatchCenter.Orders.Count}");
    if (SmartHaulersState.DispatchViewMode is DispatchDebugViewMode.Agents) {
      foreach (var agent in dispatchCenter.Agents.Where(MatchesAgentFilter).OrderBy(agent => agent.EntityId)) {
        lines.Add(FormatAgent(agent));
      }
    }
    if (SmartHaulersState.DispatchViewMode is DispatchDebugViewMode.Orders) {
      foreach (var order in OrdersForDisplay(dispatchCenter.Orders)) {
        lines.Add(FormatOrder(order));
      }
    }
  }

  static string FormatViewTitle() {
    return SmartHaulersState.DispatchViewMode switch {
        DispatchDebugViewMode.Agents => $"{DispatchDebugViewMode.Agents} / {SmartHaulersState.AgentFilter}",
        DispatchDebugViewMode.Orders => $"{DispatchDebugViewMode.Orders} / {SmartHaulersState.OrderFilter}",
        _ => SmartHaulersState.DispatchViewMode.ToString(),
    };
  }

  static bool MatchesAgentFilter(TransportAgentSnapshot agent) {
    return SmartHaulersState.AgentFilter switch {
        DispatchAgentFilter.All => true,
        DispatchAgentFilter.Candidates => IsCandidateState(agent) && !agent.RefusesWork,
        DispatchAgentFilter.Active => agent.State == TransportAgentState.Transporting,
        DispatchAgentFilter.Needs => agent.State == TransportAgentState.SatisfyingNeed,
        DispatchAgentFilter.Busy => agent.State == TransportAgentState.Working,
        DispatchAgentFilter.Haulers => agent.Role == TransportAgentRole.DedicatedHauler,
        DispatchAgentFilter.Builders => agent.Role == TransportAgentRole.Builder,
        DispatchAgentFilter.Production => agent.Role == TransportAgentRole.Production,
        DispatchAgentFilter.Resource => agent.Role == TransportAgentRole.SpecializedResource,
        DispatchAgentFilter.Community => agent.Role == TransportAgentRole.CommunityService,
        DispatchAgentFilter.Other => IsOtherAgent(agent),
        _ => true,
    };
  }

  static bool IsCandidateState(TransportAgentSnapshot agent) {
    return agent.State is TransportAgentState.Available
        or TransportAgentState.IdleWandering
        or TransportAgentState.WorkplaceIdle;
  }

  static bool IsOtherAgent(TransportAgentSnapshot agent) {
    return agent.Role is TransportAgentRole.None or TransportAgentRole.Free or TransportAgentRole.Unknown;
  }

  static bool MatchesOrderFilter(TransportOrderSnapshot order) {
    return SmartHaulersState.OrderFilter switch {
        DispatchOrderFilter.All => true,
        DispatchOrderFilter.Queued => order.Phase == OrderPhase.Queued,
        DispatchOrderFilter.Estimated => order.Phase == OrderPhase.Estimated,
        DispatchOrderFilter.Deferred => order.Phase == OrderPhase.Deferred,
        DispatchOrderFilter.Dispatchable => order.Phase == OrderPhase.Dispatchable,
        DispatchOrderFilter.Covered => order.Phase == OrderPhase.Covered,
        DispatchOrderFilter.Active => order.Phase is OrderPhase.PickingUp or OrderPhase.Delivering,
        DispatchOrderFilter.PickingUp => order.Phase == OrderPhase.PickingUp,
        DispatchOrderFilter.Delivering => order.Phase == OrderPhase.Delivering,
        _ => true,
    };
  }

  static IEnumerable<TransportOrderSnapshot> OrdersForDisplay(IEnumerable<TransportOrderSnapshot> orders) {
    var filteredOrders = orders.Where(MatchesOrderFilter);
    if (SmartHaulersState.OrderFilter == DispatchOrderFilter.All) {
      return filteredOrders;
    }
    return filteredOrders
        .OrderByDescending(order => order.Weight)
        .ThenBy(order => order.Phase)
        .ThenBy(order => order.BehaviorName)
        .ThenBy(order => order.RequesterId);
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
    return $"  {TransportDebugFormatter.FormatOrderSource(order)}, "
        + $"{TransportAgentSnapshot.FormatWorker(order.Worker)}, {order.Phase}, {order.GoodAmount}, "
        + $"{TransportDebugFormatter.FormatRoute(order)}, "
        + $"{order.RouteDistance:0.##}, {order.RemainingDistance:0.##}";
  }

  static void LogSnapshot(string text) {
    DebugEx.Info(
        "SmartHaulers snapshot columns: perf mode: T/PU/DL and phase timings | agents/orders modes: view, district, "
        + "agents, available, wandering, workplaceIdle, transporting, satisfyingNeed, working, orders | agent, "
        + "state/role, activity, position, speed, capacity | source, agent, phase, good, path, route, left | "
        + "source, phase(weight), behavior, optional good, optional path, decision, requester");
    DebugEx.Info("SmartHaulers snapshot:\n{0}", text);
  }

  static string FormatUnassignedOrder(TransportOrderSnapshot order) {
    var text = $"{TransportDebugFormatter.FormatOrderSource(order)}, "
        + $"{TransportDebugFormatter.FormatPhase(order)}, {order.BehaviorName}";
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
