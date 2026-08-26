// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;

namespace IgorZ.TimberCommons.ModUIPatches;

static class ModUIPatchesPatchOwners {
  const string TimberCommonsOwnerPrefix = "IgorZ.TimberCommons.ModUIPatches.";

  public static bool HasConflictingOwner(IEnumerable<string> owners) {
    return owners.Any(owner => !IsTimberCommonsOwner(owner));
  }

  public static bool IsTimberCommonsOwner(string owner) {
    return !string.IsNullOrEmpty(owner)
        && owner.StartsWith(TimberCommonsOwnerPrefix, StringComparison.Ordinal);
  }
}
