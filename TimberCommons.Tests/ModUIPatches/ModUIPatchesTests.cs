using IgorZ.TimberCommons.ModUIPatches;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;

namespace TimberCommons.Tests;

static class ModUIPatchesTests {
  public static void SelectsExactInactiveModVersionBeforeSource() {
    var requested = new ModReference("Example.Mod", "Example", "1.0.0");
    var userMod = CreateMod("Example.Mod", "0.9.0", isUserMod: true);
    var exactWorkshopMod = CreateMod("Example.Mod", "1.0.0", isUserMod: false);

    var selected = InactiveModSelector.Select(requested, [userMod, exactWorkshopMod]);

    Assert.Same(exactWorkshopMod, selected);
  }

  public static void SelectsUserModWhenVersionsMatch() {
    var requested = new ModReference("Example.Mod", "Example", "1.0.0");
    var workshopMod = CreateMod("Example.Mod", "1.0.0", isUserMod: false);
    var userMod = CreateMod("Example.Mod", "1.0.0", isUserMod: true);

    var selected = InactiveModSelector.Select(requested, [workshopMod, userMod]);

    Assert.Same(userMod, selected);
  }

  public static void IgnoresEnabledAndUnrelatedMods() {
    var requested = new ModReference("Example.Mod", "Example", "1.0.0");
    var enabledMod = CreateMod("Example.Mod", "1.0.0", isUserMod: true, isEnabled: true);
    var unrelatedMod = CreateMod("Other.Mod", "1.0.0", isUserMod: true);

    Assert.Equal<Mod>(null, InactiveModSelector.Select(requested, [enabledMod, unrelatedMod]));
  }

  public static void DistinguishesOwnAndConflictingPatchOwners() {
    Assert.True(ModUIPatchesPatchOwners.IsTimberCommonsOwner("IgorZ.TimberCommons.ModUIPatches.Configurator"));
    Assert.False(ModUIPatchesPatchOwners.HasConflictingOwner([
        "IgorZ.TimberCommons.ModUIPatches.Configurator",
    ]));
    Assert.True(ModUIPatchesPatchOwners.HasConflictingOwner([
        "IgorZ.TimberCommons.ModUIPatches.Configurator",
        "Other.Mod.SaveDialogPatch",
    ]));
  }

  static Mod CreateMod(string id, string version, bool isUserMod, bool isEnabled = false) {
    return new Mod {
        DisplayName = id,
        IsEnabled = isEnabled,
        ModDirectory = new ModDirectory { IsUserMod = isUserMod },
        Manifest = new ModManifest {
            Id = id,
            Version = new ModVersion { Full = version },
        },
    };
  }
}
