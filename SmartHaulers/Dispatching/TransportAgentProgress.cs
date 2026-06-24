// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Goods;
using Timberborn.InventorySystem;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportAgentProgress {
  public OrderPhase Phase { get; }
  public Inventory TargetInventory { get; }
  public string GoodId { get; }
  public int Amount { get; }

  readonly float _initialRemainingDistance;

  public TransportAgentProgress(
      OrderPhase phase, Inventory targetInventory, GoodAmount goodAmount, float initialRemainingDistance) {
    Phase = phase;
    TargetInventory = targetInventory;
    GoodId = goodAmount.GoodId;
    Amount = goodAmount.Amount;
    _initialRemainingDistance = initialRemainingDistance;
  }

  public float ProgressFrom(float remainingDistance) {
    return Mathf.Max(0f, _initialRemainingDistance - remainingDistance);
  }
}
