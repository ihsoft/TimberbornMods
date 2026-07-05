// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WaterSystem;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

sealed class ThrottlingValveFragmentLocInitializer : ILoadableSingleton {
  public ThrottlingValveFragmentLocInitializer(ILoc loc, IThreadSafeWaterMap threadSafeWaterMap) {
    ThrottlingValveFragmentPatch.SetServices(loc, threadSafeWaterMap);
  }

  public void Load() {
  }
}
