// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportOrderOrigin {
  public TransportOrderOriginType Type { get; }
  public Guid RequesterId { get; }
  public BaseComponent Requester { get; }
  public string BehaviorName { get; }
  public float Weight { get; }
  public TransportOrderDomain Domain => Type switch {
      TransportOrderOriginType.ConstructionJob => TransportOrderDomain.Construction,
      TransportOrderOriginType.HaulBehavior when BehaviorName == "BringNutrient" =>
          TransportOrderDomain.CommunityService,
      TransportOrderOriginType.HaulBehavior => TransportOrderDomain.Hauling,
      _ => TransportOrderDomain.Active,
  };
  public bool HasRequester => RequesterId != Guid.Empty && Requester;

  TransportOrderOrigin(
      TransportOrderOriginType type, Guid requesterId, BaseComponent requester, string behaviorName, float weight) {
    Type = type;
    RequesterId = requesterId;
    Requester = requester;
    BehaviorName = behaviorName;
    Weight = weight;
  }

  public static TransportOrderOrigin ActiveReservation() {
    return new TransportOrderOrigin(
        TransportOrderOriginType.ActiveReservation, Guid.Empty, null, null, float.NaN);
  }

  public static TransportOrderOrigin HaulBehavior(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight) {
    return new TransportOrderOrigin(
        TransportOrderOriginType.HaulBehavior, requesterId, requester, behaviorName, weight);
  }

  public static TransportOrderOrigin ConstructionJob(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight) {
    return new TransportOrderOrigin(
        TransportOrderOriginType.ConstructionJob, requesterId, requester, behaviorName, weight);
  }
}
