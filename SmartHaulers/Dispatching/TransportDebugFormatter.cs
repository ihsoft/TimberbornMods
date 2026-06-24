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

  public static string FormatOrderVerbose(TransportOrderSnapshot order) {
    return $"agent={TransportAgentSnapshot.FormatWorker(order.Worker)}, phase={order.Phase}, "
        + $"good={order.GoodAmount}, source={DebugEx.ObjectToString(order.Source)}, "
        + $"target={DebugEx.ObjectToString(order.Target)}, route={order.RouteDistance:0.##}, "
        + $"remaining={order.RemainingDistance:0.##}, progress={order.Progress:0.##}";
  }
}
