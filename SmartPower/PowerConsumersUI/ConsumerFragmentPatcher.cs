// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.SmartPower.PowerConsumers;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace IgorZ.SmartPower.PowerConsumersUI;

sealed class ConsumerFragmentPatcher {

  const string NotEnoughPowerLocKey = "IgorZ.SmartPower.PowerInputLimiter.NotEnoughPowerStatus";
  const string LowBatteriesChargeLocKey = "IgorZ.SmartPower.PowerInputLimiter.LowBatteriesChargeStatus";
  const string MinutesTillResumeLocKey = "IgorZ.SmartPower.Common.MinutesTillResume";
  const string MinutesTillSuspendLocKey = "IgorZ.SmartPower.Common.MinutesTillSuspend";

  readonly UiFactory _uiFactory;

  Label _suspendReasonLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _suspendReasonPatcher;
  Label _suspendStateProgressLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _suspendStateProgressPatcher;
  Label _idleStateLabel;  // It is patched in the stock UI.
  PanelFragmentPatcher _idleStatePatcher;
  bool _isInitialized;

  ConsumerFragmentPatcher(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  /// <summary>Installs the patches to the stock UI. Call it from the "ShowFragment" method.</summary>
  /// <param name="root">The root element that has been attached to the panel already.</param>
  public void InitializePatch(VisualElement root) {
    if (_isInitialized) {
      return;
    }
    _isInitialized = true;

    _suspendReasonLabel = _uiFactory.CreateLabel();
    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    _suspendReasonPatcher = new PanelFragmentPatcher(
        _suspendReasonLabel, root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Consumer");
    _suspendReasonPatcher.Patch();

    _suspendStateProgressLabel = _uiFactory.CreateLabel();
    _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
    _suspendStateProgressPatcher = new PanelFragmentPatcher(
        _suspendStateProgressLabel, root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Consumer", 1);
    _suspendStateProgressPatcher.Patch();

    _idleStateLabel = _uiFactory.CreateLabel();
    _idleStateLabel.ToggleDisplayStyle(visible: false);
    _idleStatePatcher = new PanelFragmentPatcher(
        _idleStateLabel, root, PanelFragmentPatcher.MechanicalNodeFragmentName, "Consumer", 2);
    _idleStatePatcher.Patch();
  }

  /// <summary>Hides all the elements.</summary>
  public void HideAllElements() {
    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    _idleStateLabel.ToggleDisplayStyle(visible: false);
    _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
  }

  /// <summary>Updates the power input limiter stats in UI.</summary>
  /// <param name="powerInputLimiter">
  /// The limiter to update the state for or "null", which will result imn hiding the elements.
  /// </param>
  public void UpdatePowerInputLimiter(PowerInputLimiter powerInputLimiter) {
    if (!powerInputLimiter) {
      _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
      _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
      return;
    }

    if (powerInputLimiter.IsSuspended) {
      _suspendReasonLabel.text = powerInputLimiter.LowBatteriesCharge
          ? _uiFactory.Loc.T(LowBatteriesChargeLocKey)
          : _uiFactory.Loc.T(NotEnoughPowerLocKey);
      _suspendReasonLabel.ToggleDisplayStyle(visible: true);
      if (powerInputLimiter.MinutesTillResume > 0) {
        _suspendStateProgressLabel.text = _uiFactory.Loc.T(MinutesTillResumeLocKey, powerInputLimiter.MinutesTillResume);
        _suspendStateProgressLabel.ToggleDisplayStyle(visible: true);
      } else {
        _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
      }
      return;
    }

    _suspendReasonLabel.ToggleDisplayStyle(visible: false);
    if (powerInputLimiter.MinutesTillSuspend > 0) {
      _suspendStateProgressLabel.text = _uiFactory.Loc.T(MinutesTillSuspendLocKey, powerInputLimiter.MinutesTillSuspend);
      _suspendStateProgressLabel.ToggleDisplayStyle(visible: true);
    } else {
      _suspendStateProgressLabel.ToggleDisplayStyle(visible: false);
    }
  }
}
