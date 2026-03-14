using HarmonyLib;

namespace TestParser.Stubs.Patches;

// ReSharper disable Unity.IncorrectMonoBehaviourInstantiation
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
// ReSharper disable InconsistentNaming

static class PatchStubs {
  static readonly string PatchId = typeof(PatchStubs).AssemblyQualifiedName;

  public static void Apply() {
    var harmony = new Harmony(PatchId);
    harmony.PatchAll();
  }
}