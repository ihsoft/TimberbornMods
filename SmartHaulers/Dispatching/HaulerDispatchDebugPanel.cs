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
  ResizableDropdownElement _viewDropdown;
  ResizableDropdownElement _filterDropdown;
  VisualElement _contentContainer;
  ScrollView _scrollView;
  bool _isAddedToLayout;
  bool _updatingViewDropdown;
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
    _uiFactory.AddTimberDevStylesheet(_root);
    _titleLabel = CreateTitleLabel();
    _viewDropdown = CreateViewDropdown();
    _filterDropdown = CreateFilterDropdown();
    _contentContainer = new VisualElement();
    _scrollView = CreateScrollView();
    _scrollView.contentContainer.Add(_contentContainer);
    _root.Add(_titleLabel);
    _root.Add(CreateControlsRow());
    _root.Add(_scrollView);
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
    UpdateControlDropdowns();
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
    root.style.flexDirection = FlexDirection.Column;
    root.style.left = 8;
    root.style.top = new StyleLength(new Length(38, LengthUnit.Percent));
    root.style.width = 760;
    root.style.height = new StyleLength(new Length(55, LengthUnit.Percent));
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

  VisualElement CreateControlsRow() {
    var row = new VisualElement();
    row.style.flexDirection = FlexDirection.Row;
    row.style.alignItems = Align.Center;
    row.style.marginBottom = 4;
    row.Add(_viewDropdown);
    row.Add(_filterDropdown);
    row.Add(CreateLogButton());
    return row;
  }

  ResizableDropdownElement CreateViewDropdown() {
    var dropdown = _uiFactory.CreateSimpleDropdown(OnViewSelected);
    dropdown.AutoResizeToOptions = false;
    dropdown.Items = FilterItems<DispatchDebugViewMode>();
    dropdown.style.width = 110;
    dropdown.style.marginRight = 6;
    return dropdown;
  }

  ResizableDropdownElement CreateFilterDropdown() {
    var dropdown = _uiFactory.CreateSimpleDropdown(OnFilterSelected);
    dropdown.AutoResizeToOptions = false;
    dropdown.style.width = 170;
    dropdown.style.marginRight = 6;
    return dropdown;
  }

  static Button CreateLogButton() {
    var button = new Button(SmartHaulersState.RequestLogSnapshot) {
        text = "Log",
    };
    button.style.height = 24;
    button.style.minWidth = 42;
    button.style.paddingLeft = 8;
    button.style.paddingRight = 8;
    return button;
  }

  ScrollView CreateScrollView() {
    var scrollView = _uiFactory.CreateScrollView();
    // The TimberDev scroll view only gets its scrollbar visuals from the TimberDev USS asset.
    // Attach it directly here so the debug panel does not depend on stylesheet inheritance details.
    _uiFactory.AddTimberDevStylesheet(scrollView);
    scrollView.mode = ScrollViewMode.VerticalAndHorizontal;
    scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
    scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
    scrollView.style.flexGrow = 1;
    scrollView.style.minHeight = 0;
    return scrollView;
  }

  void UpdateControlDropdowns() {
    UpdateViewDropdown();
    UpdateFilterDropdown();
  }

  void UpdateViewDropdown() {
    var currentValue = SmartHaulersState.DispatchViewMode.ToString();
    if (_viewDropdown.SelectedValue == currentValue) {
      return;
    }
    _updatingViewDropdown = true;
    _viewDropdown.SelectedValue = currentValue;
    _updatingViewDropdown = false;
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

  void OnViewSelected(string value) {
    if (_updatingViewDropdown) {
      return;
    }
    if (System.Enum.TryParse<DispatchDebugViewMode>(value, out var viewMode)) {
      SmartHaulersState.SetDispatchViewMode(viewMode);
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
    lines.Add($"last total, {FormatTotal(last)}");
    lines.Add($"last top,   {FormatBreakdown(last)}");
    lines.Add($"avg total,  {FormatAverageTotal(total, sampleCount)}");
    lines.Add($"avg top,    {FormatAverageBreakdown(total, sampleCount)}");
    lines.Add($"last O, {FormatActiveOrderBreakdown(last)}");
    lines.Add($"last Q, {FormatQueuedOrderBreakdown(last)}");
    lines.Add($"last D, {FormatDecisionBreakdown(last)}");
    lines.Add($"avg O,  {FormatAverageActiveOrderBreakdown(total, sampleCount)}");
    lines.Add($"avg Q,  {FormatAverageQueuedOrderBreakdown(total, sampleCount)}");
    lines.Add($"avg D,  {FormatAverageDecisionBreakdown(total, sampleCount)}");
    lines.Add($"last counts, {FormatCounts(last)}");
    lines.Add($"avg counts,  {FormatAverageCounts(total, sampleCount)}");
    lines.Add($"last cache,  {FormatRouteCache(last)}");
    lines.Add($"avg cache,   {FormatAverageRouteCache(total, sampleCount)}");
    lines.Add("T=A+O+Q+B+R+D+S+X. X is measured total minus named top-level phases.");
    lines.Add("A agents, O active orders, Q queued orders, B construction, R readiness, D decisions, S sort.");
    lines.Add("O=AR+RP+RD+OX, Q=PR+QX, D=DR+DP+DX.");
    lines.Add("AR active route, RP/RD remaining pickup/delivery, PR planner route, DR decision route, DP decision pickup.");
    lines.Add("AG agents, AO active orders, QO queued orders, BO construction, DO decision orders, DC candidates.");
    lines.Add("RC route cache: h hits, m misses, c clears.");
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

  static string FormatTotal(DispatchPerformanceSample sample) {
    return $"T={Milliseconds(sample.TotalTicks):0.###}ms, check={Milliseconds(TopDownDeltaTicks(sample)):0.###}ms";
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

  static string FormatActiveOrderBreakdown(DispatchPerformanceSample sample) {
    return $"AR={Milliseconds(sample.ActiveRoutePathTicks):0.###}/{sample.ActiveRoutePathCalls}, "
        + $"RP={Milliseconds(sample.RemainingPickupPathTicks):0.###}/{sample.RemainingPickupPathCalls}, "
        + $"RD={Milliseconds(sample.RemainingDeliveryPathTicks):0.###}/{sample.RemainingDeliveryPathCalls}, "
        + $"OX={Milliseconds(ActiveOrderOtherTicks(sample)):0.###}ms";
  }

  static string FormatQueuedOrderBreakdown(DispatchPerformanceSample sample) {
    return $"PR={Milliseconds(sample.PlannerRoutePathTicks):0.###}/{sample.PlannerRoutePathCalls}, "
        + $"QX={Milliseconds(QueuedOrderOtherTicks(sample)):0.###}ms";
  }

  static string FormatDecisionBreakdown(DispatchPerformanceSample sample) {
    return $"DR={Milliseconds(sample.DecisionRoutePathTicks):0.###}/{sample.DecisionRoutePathCalls}, "
        + $"DP={Milliseconds(sample.DecisionPickupPathTicks):0.###}/{sample.DecisionPickupPathCalls}, "
        + $"DX={Milliseconds(DecisionOtherTicks(sample)):0.###}ms";
  }

  static string FormatCounts(DispatchPerformanceSample sample) {
    return $"AG={sample.AgentCount}, AO={sample.ActiveOrderCount}, QO={sample.QueuedOrderCount}, "
        + $"BO={sample.ConstructionOrderCount}, DO={sample.DecisionOrderCount}, DC={sample.DecisionCandidateCount}";
  }

  static string FormatRouteCache(DispatchPerformanceSample sample) {
    return $"RC=h{sample.RouteCacheHits}/m{sample.RouteCacheMisses}/c{sample.RouteCacheClears}";
  }

  static string FormatAverageTotal(DispatchPerformanceSample total, int sampleCount) {
    return $"T={Milliseconds(total.TotalTicks) / sampleCount:0.###}ms, "
        + $"check={Milliseconds(TopDownDeltaTicks(total)) / sampleCount:0.###}ms";
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

  static string FormatAverageActiveOrderBreakdown(DispatchPerformanceSample total, int sampleCount) {
    return $"AR={Milliseconds(total.ActiveRoutePathTicks) / sampleCount:0.###}/"
        + $"{(float)total.ActiveRoutePathCalls / sampleCount:0.#}, "
        + $"RP={Milliseconds(total.RemainingPickupPathTicks) / sampleCount:0.###}/"
        + $"{(float)total.RemainingPickupPathCalls / sampleCount:0.#}, "
        + $"RD={Milliseconds(total.RemainingDeliveryPathTicks) / sampleCount:0.###}/"
        + $"{(float)total.RemainingDeliveryPathCalls / sampleCount:0.#}, "
        + $"OX={Milliseconds(ActiveOrderOtherTicks(total)) / sampleCount:0.###}ms";
  }

  static string FormatAverageQueuedOrderBreakdown(DispatchPerformanceSample total, int sampleCount) {
    return $"PR={Milliseconds(total.PlannerRoutePathTicks) / sampleCount:0.###}/"
        + $"{(float)total.PlannerRoutePathCalls / sampleCount:0.#}, "
        + $"QX={Milliseconds(QueuedOrderOtherTicks(total)) / sampleCount:0.###}ms";
  }

  static string FormatAverageDecisionBreakdown(DispatchPerformanceSample total, int sampleCount) {
    return $"DR={Milliseconds(total.DecisionRoutePathTicks) / sampleCount:0.###}/"
        + $"{(float)total.DecisionRoutePathCalls / sampleCount:0.#}, "
        + $"DP={Milliseconds(total.DecisionPickupPathTicks) / sampleCount:0.###}/"
        + $"{(float)total.DecisionPickupPathCalls / sampleCount:0.#}, "
        + $"DX={Milliseconds(DecisionOtherTicks(total)) / sampleCount:0.###}ms";
  }

  static string FormatAverageCounts(DispatchPerformanceSample total, int sampleCount) {
    return $"AG={(float)total.AgentCount / sampleCount:0.#}, "
        + $"AO={(float)total.ActiveOrderCount / sampleCount:0.#}, "
        + $"QO={(float)total.QueuedOrderCount / sampleCount:0.#}, "
        + $"BO={(float)total.ConstructionOrderCount / sampleCount:0.#}, "
        + $"DO={(float)total.DecisionOrderCount / sampleCount:0.#}, "
        + $"DC={(float)total.DecisionCandidateCount / sampleCount:0.#}";
  }

  static string FormatAverageRouteCache(DispatchPerformanceSample total, int sampleCount) {
    return $"RC=h{(float)total.RouteCacheHits / sampleCount:0.#}/m"
        + $"{(float)total.RouteCacheMisses / sampleCount:0.#}/c"
        + $"{(float)total.RouteCacheClears / sampleCount:0.#}";
  }

  static long ActiveOrderOtherTicks(DispatchPerformanceSample sample) {
    return NonNegative(
        sample.ActiveOrderTicks
        - sample.ActiveRoutePathTicks
        - sample.RemainingPickupPathTicks
        - sample.RemainingDeliveryPathTicks);
  }

  static long QueuedOrderOtherTicks(DispatchPerformanceSample sample) {
    return NonNegative(sample.QueuedOrderTicks - sample.PlannerRoutePathTicks);
  }

  static long DecisionOtherTicks(DispatchPerformanceSample sample) {
    return NonNegative(sample.DecisionTicks - sample.DecisionRoutePathTicks - sample.DecisionPickupPathTicks);
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

  static long TopDownDeltaTicks(DispatchPerformanceSample sample) {
    var topDownTicks = sample.AgentTicks
        + sample.ActiveOrderTicks
        + sample.QueuedOrderTicks
        + sample.ConstructionOrderTicks
        + sample.ReadinessTicks
        + sample.DecisionTicks
        + sample.SortTicks
        + OtherTicks(sample);
    return sample.TotalTicks - topDownTicks;
  }

  static long NonNegative(long ticks) {
    return ticks > 0 ? ticks : 0;
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
        "SmartHaulers snapshot columns: perf mode: T/PU/DL, counts, phase timings, inner path timings | "
        + "agents/orders modes: view, district, "
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
