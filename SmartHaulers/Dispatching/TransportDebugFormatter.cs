// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportDebugFormatter {
  public static string FormatAgentVerbose(TransportAgentSnapshot agent) {
    return $"{agent.DisplayName}, state={agent.State}, act={agent.Activity}, pos={agent.Position}, "
        + $"speed={agent.Speed:0.##}, cap={agent.Capacity}";
  }

  public static string FormatOrderVerbose(TransportOrderSnapshot order, bool includeRequester = true) {
    if (IsUnassignedOrder(order.Phase)) {
      var text = $"{order.Phase}, weight={order.Weight:0.##}, beh={order.BehaviorName}, "
          + $"good={order.GoodAmount}, {FormatRoute(order)}";
      return includeRequester ? $"{text}, req={FormatObject(order.Requester)}" : text;
    }
    return $"{TransportAgentSnapshot.FormatWorker(order.Worker)}, {order.Phase}, "
        + $"good={order.GoodAmount}, {FormatRoute(order)}, route={order.RouteDistance:0.##}, "
        + $"left={order.RemainingDistance:0.##}, prog={order.Progress:0.##}";
  }

  public static string FormatRoute(TransportOrderSnapshot order) {
    return $"{FormatObject(order.Source)}=>{FormatObject(order.Target)}";
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

  static string FormatComponentName(BaseComponent component) {
    var name = component.Name;
    var factionSeparator = name.IndexOf('.');
    if (factionSeparator >= 0) {
      name = name[..factionSeparator];
    }
    const string cloneSuffix = "(Clone)";
    return name.EndsWith(cloneSuffix) ? name[..^cloneSuffix.Length] : name;
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Covered or OrderPhase.Estimated;
  }
}
