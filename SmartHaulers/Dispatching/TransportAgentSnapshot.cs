// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.Beavers;
using Timberborn.Bots;
using Timberborn.Characters;
using Timberborn.WorkSystem;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportAgentSnapshot {
  public Guid EntityId { get; }
  public Worker Worker { get; }
  public string DisplayName { get; }
  public Vector3Int Position { get; }
  public Vector3 WorldPosition { get; }
  public float Speed { get; }
  public int Capacity { get; }
  public TransportAgentState State { get; }
  public TransportAgentRole Role { get; }
  public TransportWorkplaceRole WorkplaceRole { get; }
  public TransportAgentActivity Activity { get; }
  public bool RefusesWork { get; }
  public bool IsTransportAgent { get; }

  public TransportAgentSnapshot(
      Guid entityId, Worker worker, string displayName, Vector3Int position, Vector3 worldPosition, float speed,
      int capacity, TransportAgentState state, TransportAgentRole role, TransportWorkplaceRole workplaceRole,
      TransportAgentActivity activity, bool refusesWork, bool isTransportAgent) {
    EntityId = entityId;
    Worker = worker;
    DisplayName = displayName;
    Position = position;
    WorldPosition = worldPosition;
    Speed = speed;
    Capacity = capacity;
    State = state;
    Role = role;
    WorkplaceRole = workplaceRole;
    Activity = activity;
    RefusesWork = refusesWork;
    IsTransportAgent = isTransportAgent;
  }

  public static TransportAgentSnapshot NotTransportAgent(Worker worker) {
    return new TransportAgentSnapshot(
        Guid.Empty, worker, FormatWorker(worker), default, default, 0f, 0, TransportAgentState.Available,
        TransportAgentRole.None, TransportWorkplaceRole.None, TransportAgentActivity.Idle, refusesWork: false,
        isTransportAgent: false);
  }

  public static string FormatWorker(Worker worker) {
    var character = worker.GetComponent<Character>();
    var name = character ? character.FirstName : worker.Name;
    if (worker.GetComponent<Bot>()) {
      return $"bot {name}";
    }
    if (worker.GetComponent<Beaver>()) {
      return $"beaver {name}";
    }
    return name;
  }
}
