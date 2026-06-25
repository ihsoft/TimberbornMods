// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportDecisionEvaluator(TransportDistanceEstimator distanceEstimator) {
  public TransportDecision Evaluate(
      TransportOrderSnapshot order, IReadOnlyList<TransportAgentSnapshot> agents) {
    if (order.Phase != OrderPhase.Estimated || !order.Route.HasKnownEndpoints || !order.Cargo.HasGoods) {
      return default;
    }
    if (!distanceEstimator.TryGetRouteDistance(order.Source, order.Target, out var routeDistance)) {
      return default;
    }
    TransportCandidateScore winner = default;
    TransportCandidateScore runnerUp = default;
    foreach (var agent in agents) {
      if (!TryScore(order, agent, routeDistance, out var score)) {
        continue;
      }
      if (!winner.Agent.Worker || score.Score < winner.Score) {
        runnerUp = winner;
        winner = score;
      } else if (!runnerUp.Agent.Worker || score.Score < runnerUp.Score) {
        runnerUp = score;
      }
    }
    return winner.Agent.Worker ? new TransportDecision(winner, runnerUp) : default;
  }

  bool TryScore(
      TransportOrderSnapshot order, TransportAgentSnapshot agent, float routeDistance,
      out TransportCandidateScore score) {
    score = default;
    if (!IsCandidate(agent)
        || !distanceEstimator.TryGetDistanceToInventory(order.Source, agent.WorldPosition, out var distance)) {
      return false;
    }
    var pickupEta = distance / agent.Speed;
    var deliveryEta = routeDistance / agent.Speed;
    var statePenalty = StatePenalty(agent.State);
    var capacityPenalty = CapacityPenalty(order.Cargo.Amount, agent.Capacity);
    var carryAmount = Math.Min(order.Cargo.Amount, agent.Capacity);
    score = new TransportCandidateScore(
        agent, distance, pickupEta, deliveryEta, statePenalty, capacityPenalty, carryAmount,
        pickupEta + deliveryEta + statePenalty + capacityPenalty);
    return true;
  }

  static bool IsCandidate(TransportAgentSnapshot agent) {
    return agent.IsTransportAgent
        && agent.Worker
        && agent.Speed > 0f
        && agent.Capacity > 0
        && (agent.State is TransportAgentState.Available
            or TransportAgentState.IdleWandering
            or TransportAgentState.WorkplaceIdle);
  }

  static float StatePenalty(TransportAgentState state) {
    return state switch {
        TransportAgentState.Available => 0f,
        TransportAgentState.IdleWandering => 1f,
        TransportAgentState.WorkplaceIdle => 2f,
        _ => 1000f,
    };
  }

  static float CapacityPenalty(int requestedAmount, int capacity) {
    return Math.Max(0, requestedAmount - capacity) * 0.25f;
  }
}
