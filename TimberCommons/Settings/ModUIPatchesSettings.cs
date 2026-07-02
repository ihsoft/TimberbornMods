// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class ModUIPatchesSettings : BaseSettings<ModUIPatchesSettings> {
  const string HeaderStringLocKey = "IgorZ.TimberCommons.Settings.ModUIPatchesSection";
  const string EnhanceSaveModsIncompatibilityDialogLocKey =
      "IgorZ.TimberCommons.Settings.ModUIPatches.EnhanceSaveModsIncompatibilityDialog";
  const string EnhanceSaveModsIncompatibilityDialogTooltipLocKey =
      "IgorZ.TimberCommons.Settings.ModUIPatches.EnhanceSaveModsIncompatibilityDialogTooltip";
  const string TimberCommonsModUiPatchOwnerPrefix = "IgorZ.TimberCommons.ModUIPatches.";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global

  public static bool EnhanceSaveModsIncompatibilityDialog { get; private set; } = true;
  public ModSetting<bool> EnhanceSaveModsIncompatibilityDialogInternal { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized(EnhanceSaveModsIncompatibilityDialogLocKey)
          .SetLocalizedTooltip(EnhanceSaveModsIncompatibilityDialogTooltipLocKey)
          .SetEnableCondition(() => !HasConflictingSaveModsIncompatibilityDialogPatch()));

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 5;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region API

  public static bool ShouldEnhanceSaveModsIncompatibilityDialog() {
    return EnhanceSaveModsIncompatibilityDialog && !HasConflictingSaveModsIncompatibilityDialogPatch();
  }

  #endregion

  #region Implementation

  public ModUIPatchesSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(
        EnhanceSaveModsIncompatibilityDialogInternal, v => EnhanceSaveModsIncompatibilityDialog = v);
  }

  static bool HasConflictingSaveModsIncompatibilityDialogPatch() {
    var method = AccessTools.Method(
        typeof(SaveModsValidator),
        nameof(SaveModsValidator.ShowModsIncompatibilityDialog));
    var patchInfo = Harmony.GetPatchInfo(method);
    return patchInfo != null
        && HasConflictingPatchOwner(patchInfo.Prefixes.Select(patch => patch.owner)
            .Concat(patchInfo.Postfixes.Select(patch => patch.owner))
            .Concat(patchInfo.Transpilers.Select(patch => patch.owner))
            .Concat(patchInfo.Finalizers.Select(patch => patch.owner)));
  }

  static bool HasConflictingPatchOwner(IEnumerable<string> patchOwners) {
    return patchOwners.Any(owner => !IsTimberCommonsModUiPatchOwner(owner));
  }

  static bool IsTimberCommonsModUiPatchOwner(string owner) {
    return !string.IsNullOrEmpty(owner)
        && owner.StartsWith(TimberCommonsModUiPatchOwnerPrefix, StringComparison.Ordinal);
  }

  #endregion
}
