// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.Utils;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.SaveMetadataSystem;

// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.ModUIPatches;

[HarmonyPatch(typeof(SaveModsValidator), nameof(SaveModsValidator.ShowModsIncompatibilityDialog))]
static class SaveModsValidatorShowModsIncompatibilityDialogPatch {
  static bool Prefix(SaveMetadata metadata, Action continueCallback) {
    if (!ModUIPatchesSettings.ShouldEnhanceSaveModsIncompatibilityDialog()) {
      return true;
    }

    StaticBindings.DependencyContainer.GetInstance<ModsIncompatibilityDialog>()
        .WithSave(metadata, continueCallback)
        .Show();
    return false;
  }
}
