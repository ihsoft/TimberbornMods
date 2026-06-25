// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportDebugFormatter {
  public static string FormatAgentVerbose(TransportAgentSnapshot agent) {
    return $"agent={agent.DisplayName}, state={agent.State}, act={agent.Activity}, pos={agent.Position}, "
        + $"speed={agent.Speed:0.##}, cap={agent.Capacity}";
  }

  public static string FormatOrderVerbose(TransportOrderSnapshot order, bool includeRequester = true) {
    if (IsUnassignedOrder(order.Phase)) {
      var text = $"phase={order.Phase}, weight={order.Weight:0.##}, beh={order.BehaviorName}, "
          + $"good={order.GoodAmount}, from={DebugEx.ObjectToString(order.Source)}, "
          + $"to={DebugEx.ObjectToString(order.Target)}";
      return includeRequester ? $"{text}, req={DebugEx.ObjectToString(order.Requester)}" : text;
    }
    return $"agent={TransportAgentSnapshot.FormatWorker(order.Worker)}, phase={order.Phase}, "
        + $"good={order.GoodAmount}, from={DebugEx.ObjectToString(order.Source)}, "
        + $"to={DebugEx.ObjectToString(order.Target)}, route={order.RouteDistance:0.##}, "
        + $"left={order.RemainingDistance:0.##}, prog={order.Progress:0.##}";
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Covered or OrderPhase.Estimated;
  }
}
