// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class TransportOrderMemory {
  public Inventory Source { get; private set; }
  public Inventory Target { get; private set; }
  public string GoodId { get; private set; }
  public int Amount { get; private set; }

  public bool Matches(GoodAmount goodAmount) {
    return GoodId == goodAmount.GoodId && Amount == goodAmount.Amount;
  }

  public void Update(Inventory source, Inventory target, GoodAmount goodAmount) {
    if (source) {
      Source = source;
    }
    if (target) {
      Target = target;
    }
    GoodId = goodAmount.GoodId;
    Amount = goodAmount.Amount;
  }
}
