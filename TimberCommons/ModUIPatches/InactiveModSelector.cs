// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;

namespace IgorZ.TimberCommons.ModUIPatches;

static class InactiveModSelector {
  public static Mod Select(ModReference modReference, IEnumerable<Mod> mods) {
    return mods
        .Where(mod => mod.Manifest.Id == modReference.Id && !mod.IsEnabled)
        .OrderByDescending(mod => mod.Manifest.Version.Full == modReference.Version)
        .ThenByDescending(mod => mod.ModDirectory.IsUserMod)
        .ThenBy(mod => mod.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
  }
}
