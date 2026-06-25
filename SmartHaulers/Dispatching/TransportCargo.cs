// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Goods;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportCargo {
  public GoodAmount GoodAmount { get; }
  public string GoodId => GoodAmount.GoodId;
  public int Amount => GoodAmount.Amount;
  public bool HasGoods => !string.IsNullOrEmpty(GoodAmount.GoodId) && GoodAmount.Amount > 0;

  public TransportCargo(GoodAmount goodAmount) {
    GoodAmount = goodAmount;
  }

  public override string ToString() {
    return GoodAmount.ToString();
  }
}
