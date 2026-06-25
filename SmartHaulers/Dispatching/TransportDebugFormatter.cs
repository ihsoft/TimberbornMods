// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportDebugFormatter {
  public static string FormatAgentVerbose(TransportAgentSnapshot agent) {
    return $"worker={agent.DisplayName}, state={agent.State}, activity={agent.Activity}, position={agent.Position}, "
        + $"speed={agent.Speed:0.##}, capacity={agent.Capacity}";
  }

  public static string FormatOrderVerbose(TransportOrderSnapshot order, bool includeRequester = true) {
    if (IsUnassignedOrder(order.Phase)) {
      var text = $"phase={order.Phase}, weight={order.Weight:0.##}, behavior={order.BehaviorName}, "
          + $"good={order.GoodAmount}, source={DebugEx.ObjectToString(order.Source)}, "
          + $"target={DebugEx.ObjectToString(order.Target)}";
      return includeRequester ? $"{text}, requester={DebugEx.ObjectToString(order.Requester)}" : text;
    }
    return $"agent={TransportAgentSnapshot.FormatWorker(order.Worker)}, phase={order.Phase}, "
        + $"good={order.GoodAmount}, source={DebugEx.ObjectToString(order.Source)}, "
        + $"target={DebugEx.ObjectToString(order.Target)}, route={order.RouteDistance:0.##}, "
        + $"remaining={order.RemainingDistance:0.##}, progress={order.Progress:0.##}";
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Covered or OrderPhase.Estimated;
  }
}
