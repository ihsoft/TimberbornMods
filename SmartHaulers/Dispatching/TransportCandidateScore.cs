// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportCandidateScore {
  public TransportAgentSnapshot Agent { get; }
  public float DistanceToSource { get; }
  public float RouteDistance { get; }
  public float PickupEta { get; }
  public float DeliveryEta { get; }
  public float TotalEta => PickupEta + DeliveryEta;
  public float CapacityRatio { get; }
  public float StatePenalty { get; }
  public float CapacityPenalty { get; }
  public int CarryAmount { get; }
  public int CarryWeight { get; }
  public int RequestedWeight { get; }
  public string StateClass { get; }
  public float Score { get; }

  public TransportCandidateScore(
      TransportAgentSnapshot agent, float distanceToSource, float routeDistance, float pickupEta, float deliveryEta,
      float capacityRatio, float statePenalty, float capacityPenalty, int carryAmount, int carryWeight,
      int requestedWeight, string stateClass, float score) {
    Agent = agent;
    DistanceToSource = distanceToSource;
    RouteDistance = routeDistance;
    PickupEta = pickupEta;
    DeliveryEta = deliveryEta;
    CapacityRatio = capacityRatio;
    StatePenalty = statePenalty;
    CapacityPenalty = capacityPenalty;
    CarryAmount = carryAmount;
    CarryWeight = carryWeight;
    RequestedWeight = requestedWeight;
    StateClass = stateClass;
    Score = score;
  }
}
