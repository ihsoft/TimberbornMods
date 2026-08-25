// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;
using Timberborn.SingletonSystem;

namespace IgorZ.TimberCommons.CommonUIPatches;

sealed class ModListViewLocInitializer : ILoadableSingleton {
  public ModListViewLocInitializer(ILoc loc) {
    ModListViewInitializePatch.SetLoc(loc);
  }

  public void Load() {
  }
}
