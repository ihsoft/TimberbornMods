// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.SingletonSystem;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

sealed class IrrigationTowerOutputInitializer : ILoadableSingleton {
  IrrigationTowerOutputInitializer(IrrigationTowerOutputFactory irrigationTowerOutputFactory) {
    GoodConsumingIrrigationTowerOutputPatch.Initialize(irrigationTowerOutputFactory);
    ManufactoryIrrigationTowerOutputPatch.Initialize(irrigationTowerOutputFactory);
  }

  public void Load() {
  }
}
