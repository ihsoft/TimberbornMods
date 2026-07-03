// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportDebugFormatter {
  public static string FormatAgentVerbose(TransportAgentSnapshot agent) {
    return $"{agent.DisplayName}, state={FormatAgentState(agent)}, act={agent.Activity}, pos={agent.Position}, "
        + $"speed={agent.Speed:0.##}, cap={agent.Capacity}";
  }

  public static string FormatOrderVerbose(TransportOrderSnapshot order, bool includeRequester = true) {
    if (IsUnassignedOrder(order.Phase)) {
      var text = $"{FormatOrderSource(order)}, {FormatPhase(order)}, {order.BehaviorName}";
      text = AppendPart(text, FormatCargo(order));
      text = AppendPart(text, FormatKnownRoute(order));
      text += FormatDecision(order.Decision);
      return includeRequester ? $"{text}, req={FormatObject(order.Requester)}" : text;
    }
    return $"{FormatOrderSource(order)}, {TransportAgentSnapshot.FormatWorker(order.Worker)}, {order.Phase}, "
        + $"{order.GoodAmount}, {FormatRoute(order)}, route={order.RouteDistance:0.##}, "
        + $"left={order.RemainingDistance:0.##}";
  }

  public static string FormatOrderSource(TransportOrderSnapshot order) {
    return order.Origin.Type switch {
        TransportOrderOriginType.ActiveReservation => "GAME",
        TransportOrderOriginType.ConstructionJob => "IDEA/build",
        TransportOrderOriginType.HaulBehavior => "IDEA",
        _ => "IDEA",
    };
  }

  public static string FormatRoute(TransportOrderSnapshot order) {
    return $"{FormatObject(order.Source)}=>{FormatObject(order.Target)}";
  }

  public static string FormatKnownRoute(TransportOrderSnapshot order) {
    return order.Phase is OrderPhase.Queued or OrderPhase.Covered ? "" : FormatRoute(order);
  }

  public static string FormatPhase(TransportOrderSnapshot order) {
    if (!IsUnassignedOrder(order.Phase)) {
      return order.Phase.ToString();
    }
    var text = $"{order.Phase} (w={order.Weight:0.##}";
    if (order.HasCriticalTime) {
      text += $",t={order.CriticalTimeInHours:0.#}h";
    }
    return $"{text})";
  }

  public static string FormatDecision(TransportDecision decision) {
    if (!decision.HasWinner) {
      return "";
    }
    var text = $", best={FormatCandidate(decision.Winner)}";
    return decision.HasRunnerUp ? $"{text}, next={FormatCandidate(decision.RunnerUp)}" : text;
  }

  public static string FormatCargo(TransportOrderSnapshot order) {
    return order.Phase == OrderPhase.Queued ? "" : order.GoodAmount.ToString();
  }

  public static string FormatObject(BaseComponent component) {
    if (component == null) {
      return "[NULL]";
    }
    if (!component) {
      return "[DestroyedComponent]";
    }
    var blockObject = component.GetComponent<BlockObject>();
    if (blockObject) {
      return $"[{FormatComponentName(component)}@{blockObject.Coordinates}]";
    }
    return $"[{FormatComponentName(component)}]";
  }

  public static string FormatCandidate(TransportCandidateScore candidate) {
    return $"{candidate.Agent.DisplayName} score={candidate.Score:0.##} eta={candidate.TotalEta:0.##} "
        + $"toA={candidate.DistanceToSource:0.##}/{candidate.PickupEta:0.##} "
        + $"AtoB={candidate.RouteDistance:0.##}/{candidate.DeliveryEta:0.##} "
        + $"cap={candidate.CarryAmount}x {candidate.CarryWeight}/{candidate.RequestedWeight}kg "
        + $"({candidate.CapacityRatio:0.##}, max={candidate.Agent.Capacity}kg) "
        + $"state={candidate.StateClass}+{candidate.StatePenalty:0.##} capP={candidate.CapacityPenalty:0.##}";
  }

  static string FormatComponentName(BaseComponent component) {
    var name = component.Name;
    var factionSeparator = name.IndexOf('.');
    if (factionSeparator >= 0) {
      name = name[..factionSeparator];
    }
    const string cloneSuffix = "(Clone)";
    return name.EndsWith(cloneSuffix) ? name[..^cloneSuffix.Length] : name;
  }

  static string AppendPart(string text, string part) {
    return string.IsNullOrEmpty(part) ? text : $"{text}, {part}";
  }

  public static string FormatAgentState(TransportAgentSnapshot agent) {
    var text = agent.Role is TransportAgentRole.None or TransportAgentRole.DedicatedHauler
        ? FormatState(agent)
        : $"{FormatState(agent)}/{FormatRole(agent.Role)}";
    return agent.RefusesWork ? $"{text}/noWork" : text;
  }

  static string FormatState(TransportAgentSnapshot agent) {
    return agent.State == TransportAgentState.WorkplaceIdle
        ? $"{agent.State}/{agent.WorkplaceRole}"
        : agent.State.ToString();
  }

  static string FormatRole(TransportAgentRole role) {
    return role switch {
        TransportAgentRole.CommunityService => "community",
        TransportAgentRole.Builder => "builder",
        TransportAgentRole.Production => "production",
        TransportAgentRole.SpecializedResource => "resource",
        TransportAgentRole.Free => "free",
        TransportAgentRole.Unknown => "unknown",
        _ => role.ToString(),
    };
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Estimated or OrderPhase.Deferred or OrderPhase.Dispatchable
        or OrderPhase.Covered;
  }
}
