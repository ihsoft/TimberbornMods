// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.ApplicationLifetime;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.ModUIPatches;

sealed class ModsIncompatibilityDialog : AbstractDialog {
  const int IncompatibilityDialogBoxMaxWidth = 1200;
  const string DialogResource = "Core/DialogBox";
  const string DialogContentResource = "Modding/ModIncompatibilityDialogBox";
  const string RestartConfirmationLocKey = "IgorZ.TimberCommons.ModUIPatches.SaveModsValidator.RestartConfirmation";
  const string ContinueWithoutRestartLocKey =
      "IgorZ.TimberCommons.ModUIPatches.SaveModsValidator.ContinueWithoutRestart";
  const string EnableModTooltipLocKey = "IgorZ.TimberCommons.ModUIPatches.SaveModsValidator.EnableModTooltip";
  const string RestartButtonLocKey = "Settings.Language.Restart";

  readonly ModRepository _modRepository;
  readonly SimpleModItemFactory _simpleModItemFactory;

  PendingModsRestartState _restartState;
  Action _continueCallback;

  public ModsIncompatibilityDialog(ModRepository modRepository, SimpleModItemFactory simpleModItemFactory) {
    _modRepository = modRepository;
    _simpleModItemFactory = simpleModItemFactory;
  }

  public ModsIncompatibilityDialog WithSave(SaveMetadata metadata, Action continueCallback) {
    Metadata = metadata;
    _continueCallback = continueCallback;
    return this;
  }

  SaveMetadata Metadata { get; set; }

  #region AbstractDialog implementation

  protected override string DialogResourceName => DialogResource;

  public override void Show() {
    base.Show();
    Root.Q<VisualElement>("Box").style.maxWidth = IncompatibilityDialogBoxMaxWidth;
    Root.Q<VisualElement>("Content").Add(UiFactory.LoadVisualElement(DialogContentResource));
    Root.Q<Button>("ConfirmButton").text = UiFactory.T(CommonLocKeys.YesKey);
    Root.Q<Button>("CancelButton").text = UiFactory.T(CommonLocKeys.NoKey);
    Root.Q<Button>("InfoButton").ToggleDisplayStyle(visible: false);

    FillActiveMods(Root.Q<ScrollView>("ActiveMods"));
    var savedMods = Root.Q<ScrollView>("SavedMods");
    var savedModReferences = Metadata.Mods
        .OrderBy(mod => GetDisplayName(mod.Name), StringComparer.OrdinalIgnoreCase)
        .ThenBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
        .ThenBy(mod => mod.Version, StringComparer.OrdinalIgnoreCase)
        .ToList();
    FillSavedMods(savedMods, savedModReferences);
    _restartState = AddEnableModButtons(savedMods, savedModReferences);
  }

  protected override string VerifyInput() {
    return _restartState.Count > 0
        ? UiFactory.T(RestartConfirmationLocKey, _restartState.Count)
        : null;
  }

  protected override void ApplyInput() {
    _continueCallback();
  }

  protected override bool CheckHasChanges() {
    return false;
  }

  protected override void ShowVerificationMessage(string message) {
    DialogBox dialogBox = null!;
    dialogBox = DialogBoxShower.Create()
        .SetMessage(message)
        .SetConfirmButton(GameQuitter.Quit, UiFactory.T(RestartButtonLocKey))
        .SetInfoButton(
            () => {
              dialogBox.Close();
              ContinueWithoutRestart();
            },
            UiFactory.T(ContinueWithoutRestartLocKey))
        .SetDefaultCancelButton(UiFactory.T(CommonLocKeys.CancelKey))
        .Show();
  }

  #endregion

  #region Implementation

  void FillActiveMods(VisualElement container) {
    foreach (var enabledMod in _modRepository.EnabledMods
        .OrderBy(mod => GetDisplayName(mod.DisplayName), StringComparer.OrdinalIgnoreCase)
        .ThenBy(mod => mod.Manifest.Version.Full, StringComparer.OrdinalIgnoreCase)) {
      container.Add(
          _simpleModItemFactory.CreateModItem(
              GetDisplayName(enabledMod.DisplayName),
              enabledMod.Manifest.Version.Formatted));
    }
  }

  void FillSavedMods(VisualElement container, IReadOnlyList<ModReference> modReferences) {
    foreach (var modReference in modReferences) {
      var modItem = _simpleModItemFactory.CreateModItem(
          GetDisplayName(modReference.Name),
          Timberborn.Versioning.Version.Create(modReference.Version).Formatted);
      if (_modRepository.ModIsNotEnabled(modReference.Id)) {
        _simpleModItemFactory.SetErrorIcon(modItem);
      } else if (_modRepository.ModIsOnDifferentVersion(modReference.Id, modReference.Version)) {
        _simpleModItemFactory.SetWarningIcon(modItem);
      }
      container.Add(modItem);
    }
  }

  PendingModsRestartState AddEnableModButtons(ScrollView savedMods, IReadOnlyList<ModReference> modReferences) {
    var restartState = new PendingModsRestartState();
    if (savedMods == null) {
      return restartState;
    }

    var modItems = savedMods.contentContainer.Children().ToList();
    for (var i = 0; i < modReferences.Count && i < modItems.Count; i++) {
      var mod = GetMatchingInactiveMod(modReferences[i]);
      if (mod != null) {
        AddEnableModToggle(modItems[i], mod, restartState, _simpleModItemFactory._tooltipRegistrar);
      }
    }

    return restartState;
  }

  void AddEnableModToggle(
      VisualElement modItem, Mod mod, PendingModsRestartState restartState, ITooltipRegistrar tooltipRegistrar) {
    var toggle = new Toggle {
        name = "EnableModToggle",
    };
    toggle.AddToClassList("mod-item__toggle");
    toggle.text = "";
    toggle.style.flexShrink = 0;
    toggle.style.height = 25;
    toggle.style.marginLeft = 5;
    toggle.style.width = 25;

    var pending = ModPlayerPrefsHelper.IsModEnabled(mod);
    tooltipRegistrar.Register(
        toggle,
        () => UiFactory.T(EnableModTooltipLocKey, GetDisplayName(mod.DisplayName)));
    toggle.SetValueWithoutNotify(pending);
    restartState.SetPending(mod, pending);

    toggle.RegisterValueChangedCallback(
        evt => {
          pending = evt.newValue;
          ModPlayerPrefsHelper.ToggleMod(pending, mod);
          restartState.SetPending(mod, pending);
        });

    modItem.Add(toggle);
  }

  Mod GetMatchingInactiveMod(ModReference modReference) {
    if (!_modRepository.ModIsNotEnabled(modReference.Id)) {
      return null;
    }
    return _modRepository.Mods
        .Where(mod => mod.Manifest.Id == modReference.Id && !mod.IsEnabled)
        .OrderByDescending(mod => mod.Manifest.Version.Full == modReference.Version)
        .ThenByDescending(mod => mod.ModDirectory.IsUserMod)
        .ThenBy(mod => GetDisplayName(mod.DisplayName), StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
  }

  void ContinueWithoutRestart() {
    _continueCallback();
    Close();
  }

  static string GetDisplayName(string name) {
    return name.Trim();
  }

  sealed class PendingModsRestartState {
    readonly HashSet<Mod> _pendingMods = [];

    public int Count => _pendingMods.Count;

    public void SetPending(Mod mod, bool pending) {
      if (pending) {
        _pendingMods.Add(mod);
      } else {
        _pendingMods.Remove(mod);
      }
    }
  }

  #endregion
}
