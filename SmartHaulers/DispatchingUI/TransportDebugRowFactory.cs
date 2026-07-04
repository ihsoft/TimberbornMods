// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartHaulers.Dispatching;
using Timberborn.BaseComponentSystem;
using Timberborn.SelectionSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.SmartHaulers.DispatchingUI;

sealed class TransportDebugRowFactory(EntitySelectionService entitySelectionService) {
  static readonly Color TextColor = Color.white;
  static readonly Color LinkColor = new Color(0.55f, 0.82f, 1f);

  public VisualElement CreateAgentRow(TransportAgentSnapshot agent) {
    var row = CreateRow();
    AddLink(row, agent.DisplayName, agent.Worker);
    AddText(row, $", state={TransportDebugFormatter.FormatAgentState(agent)}, act={agent.Activity}");
    AddActivityTarget(row, agent.Activity);
    AddText(row, $", pos={agent.Position}, ");
    AddText(row, $"speed={agent.Speed:0.##}, cap={agent.Capacity}");
    return row;
  }

  void AddActivityTarget(VisualElement row, TransportAgentActivity activity) {
    if (!activity.HasTargetInfo) {
      return;
    }
    AddText(row, $", target={activity.TargetLabel}");
    if (activity.Target) {
      AddText(row, "=");
      AddLink(row, TransportDebugFormatter.FormatObject(activity.Target), activity.Target);
    }
  }

  public VisualElement CreateOrderRow(
      TransportOrderSnapshot order, bool includeAgent = true, bool includeRequester = true) {
    var row = CreateRow();
    if (IsUnassignedOrder(order.Phase)) {
      AddText(row, $"{TransportDebugFormatter.FormatOrderSource(order)}, ");
      AddText(row, $"{TransportDebugFormatter.FormatPhase(order)}, {order.BehaviorName}");
      AddOptionalText(row, TransportDebugFormatter.FormatCargo(order));
      AddOptionalRoute(row, order);
      AddDecision(row, order.Decision);
      if (includeRequester) {
        AddText(row, ", req=");
        AddLink(row, TransportDebugFormatter.FormatObject(order.Requester), order.Requester);
      }
      return row;
    }
    AddText(row, $"{TransportDebugFormatter.FormatOrderSource(order)}, ");
    if (includeAgent) {
      AddLink(row, TransportAgentSnapshot.FormatWorker(order.Worker), order.Worker);
      AddText(row, ", ");
    }
    AddText(row, $"{order.Phase}, {order.GoodAmount}, ");
    if (order.Domain == TransportOrderDomain.CriticalNeed) {
      AddText(row, "at=");
      AddLink(row, TransportDebugFormatter.FormatObject(order.Source), order.Source);
      AddText(row, $", left={order.RemainingDistance:0.##}");
      return row;
    }
    AddRoute(row, order);
    AddText(row, $", route={order.RouteDistance:0.##}, left={order.RemainingDistance:0.##}");
    return row;
  }

  void AddDecision(VisualElement row, TransportDecision decision) {
    if (!decision.HasWinner) {
      return;
    }
    AddText(row, ", best=");
    AddLink(row, TransportDebugFormatter.FormatCandidate(decision.Winner), decision.Winner.Agent.Worker);
    if (!decision.HasRunnerUp) {
      return;
    }
    AddText(row, ", next=");
    AddLink(row, TransportDebugFormatter.FormatCandidate(decision.RunnerUp), decision.RunnerUp.Agent.Worker);
  }

  static VisualElement CreateRow() {
    var row = new VisualElement();
    row.style.flexDirection = FlexDirection.Row;
    row.style.flexWrap = Wrap.Wrap;
    return row;
  }

  void AddRoute(VisualElement row, TransportOrderSnapshot order) {
    AddLink(row, TransportDebugFormatter.FormatObject(order.Source), order.Source);
    AddText(row, "=>");
    AddLink(row, TransportDebugFormatter.FormatObject(order.Target), order.Target);
  }

  void AddOptionalRoute(VisualElement row, TransportOrderSnapshot order) {
    if (order.Phase is not OrderPhase.Queued and not OrderPhase.Covered) {
      AddText(row, ", ");
      AddRoute(row, order);
    }
  }

  static void AddOptionalText(VisualElement row, string text) {
    if (!string.IsNullOrEmpty(text)) {
      AddText(row, $", {text}");
    }
  }

  void AddLink(VisualElement row, string text, BaseComponent target) {
    if (!IsSelectable(target)) {
      AddText(row, text);
      return;
    }
    var label = CreateLabel(text);
    label.style.color = LinkColor;
    label.RegisterCallback<ClickEvent>(_ => Select(target));
    row.Add(label);
  }

  static void AddText(VisualElement row, string text) {
    row.Add(CreateLabel(text));
  }

  static Label CreateLabel(string text) {
    var label = new Label(text);
    label.style.whiteSpace = WhiteSpace.Normal;
    label.style.color = TextColor;
    return label;
  }

  void Select(BaseComponent target) {
    if (IsSelectable(target)) {
      entitySelectionService.SelectAndFocusOn(target);
    }
  }

  static bool IsSelectable(BaseComponent target) {
    return target && target.HasComponent<Timberborn.EntitySystem.EntityComponent>()
        && !target.GetComponent<Timberborn.EntitySystem.EntityComponent>().Deleted;
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Estimated or OrderPhase.Deferred or OrderPhase.Dispatchable
        or OrderPhase.Covered;
  }
}
