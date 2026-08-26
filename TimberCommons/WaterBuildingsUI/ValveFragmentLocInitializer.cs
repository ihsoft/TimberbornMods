// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

sealed class ValveFragmentLocInitializer : ILoadableSingleton {
  public ValveFragmentLocInitializer(ILoc loc, IThreadSafeWaterMap threadSafeWaterMap) {
    ValveFragmentPatch.SetServices(loc, threadSafeWaterMap);
  }

  public void Load() {
  }
}
