// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.Goods;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportDecisionEvaluator(TransportDistanceEstimator distanceEstimator, IGoodService goodService) {
  public TransportDecision Evaluate(
      TransportOrderSnapshot order, IReadOnlyList<TransportAgentSnapshot> agents) {
    if (order.Phase != OrderPhase.Dispatchable || !order.Route.HasKnownEndpoints || !order.Cargo.HasGoods) {
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
    if (!IsCandidate(order, agent)
        || !distanceEstimator.TryGetDistanceToInventory(order.Source, agent.WorldPosition, out var distance)) {
      return false;
    }
    var pickupEta = distance / agent.Speed;
    var deliveryEta = routeDistance / agent.Speed;
    var statePenalty = StatePenalty(order, agent);
    var goodWeight = goodService.GetGood(order.Cargo.GoodId).Weight;
    var carryAmount = CarryAmount(order.Cargo.Amount, agent.Capacity, goodWeight);
    var requestedWeight = order.Cargo.Amount * goodWeight;
    var carryWeight = carryAmount * goodWeight;
    var capacityRatio = CapacityRatio(order.Cargo.Amount, carryAmount);
    var capacityPenalty = CapacityPenalty(capacityRatio);
    var stateClass = StateClass(agent);
    score = new TransportCandidateScore(
        agent, distance, routeDistance, pickupEta, deliveryEta, capacityRatio, statePenalty, capacityPenalty,
        carryAmount, carryWeight, requestedWeight, stateClass,
        pickupEta + deliveryEta + statePenalty + capacityPenalty);
    return true;
  }

  static bool IsCandidate(TransportOrderSnapshot order, TransportAgentSnapshot agent) {
    return agent.IsTransportAgent
        && agent.Worker
        && agent.Speed > 0f
        && agent.Capacity > 0
        && IsEligibleRole(order, agent)
        && (agent.State is TransportAgentState.Available
            or TransportAgentState.IdleWandering
            or TransportAgentState.WorkplaceIdle);
  }

  static bool IsEligibleRole(TransportOrderSnapshot order, TransportAgentSnapshot agent) {
    return order.Domain switch {
        TransportOrderDomain.Hauling => agent.Role == TransportAgentRole.DedicatedHauler,
        TransportOrderDomain.CommunityService => agent.Role
            is TransportAgentRole.CommunityService
            or TransportAgentRole.DedicatedHauler,
        TransportOrderDomain.Construction => agent.Role == TransportAgentRole.Builder,
        _ => false,
    };
  }

  static float StatePenalty(TransportOrderSnapshot order, TransportAgentSnapshot agent) {
    var statePenalty = agent.State switch {
        TransportAgentState.Available => 0f,
        TransportAgentState.IdleWandering => 1f,
        TransportAgentState.WorkplaceIdle => WorkplaceIdlePenalty(agent.WorkplaceRole),
        _ => 1000f,
    };
    return statePenalty + RolePenalty(order, agent);
  }

  static float RolePenalty(TransportOrderSnapshot order, TransportAgentSnapshot agent) {
    return order.Domain switch {
        TransportOrderDomain.CommunityService when agent.Role == TransportAgentRole.DedicatedHauler => 5f,
        _ => 0f,
    };
  }

  static float WorkplaceIdlePenalty(TransportWorkplaceRole role) {
    return role switch {
        TransportWorkplaceRole.Transport => 1f,
        TransportWorkplaceRole.Builder => 10f,
        TransportWorkplaceRole.Production => 50f,
        TransportWorkplaceRole.Unknown => 100f,
        _ => 100f,
    };
  }

  static int CarryAmount(int requestedAmount, int liftingCapacity, int goodWeight) {
    var carryCapacity = Math.Max(liftingCapacity / goodWeight, 1);
    return Math.Min(requestedAmount, carryCapacity);
  }

  static float CapacityRatio(int requestedAmount, int carryAmount) {
    return requestedAmount <= 0 ? 1f : Math.Min(1f, (float)carryAmount / requestedAmount);
  }

  static float CapacityPenalty(float capacityRatio) {
    return (1f - capacityRatio) * 2f;
  }

  static string StateClass(TransportAgentSnapshot agent) {
    var stateClass = agent.State switch {
        TransportAgentState.Available => "free",
        TransportAgentState.IdleWandering => "idle",
        TransportAgentState.WorkplaceIdle => WorkplaceIdleClass(agent.WorkplaceRole),
        _ => "busy",
    };
    return agent.Role == TransportAgentRole.DedicatedHauler ? stateClass : $"{stateClass}/{RoleClass(agent.Role)}";
  }

  static string RoleClass(TransportAgentRole role) {
    return role switch {
        TransportAgentRole.CommunityService => "community",
        TransportAgentRole.Builder => "builder",
        TransportAgentRole.Production => "production",
        TransportAgentRole.Free => "free",
        TransportAgentRole.Unknown => "unknown",
        _ => "none",
    };
  }

  static string WorkplaceIdleClass(TransportWorkplaceRole role) {
    return role switch {
        TransportWorkplaceRole.Transport => "workplace-transport",
        TransportWorkplaceRole.Builder => "workplace-builder",
        TransportWorkplaceRole.Production => "workplace-production",
        TransportWorkplaceRole.Unknown => "workplace-unknown",
        _ => "workplace-unknown",
    };
  }
}
