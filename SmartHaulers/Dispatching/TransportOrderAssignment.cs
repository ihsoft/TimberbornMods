// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.WorkSystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportOrderAssignment {
  public static readonly TransportOrderAssignment None = new TransportOrderAssignment(Guid.Empty, null);

  public Guid AgentId { get; }
  public Worker Worker { get; }
  public bool IsAssigned => AgentId != Guid.Empty && Worker;

  public TransportOrderAssignment(Guid agentId, Worker worker) {
    AgentId = agentId;
    Worker = worker;
  }
}
