// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.NeedApplication;
using Timberborn.TooltipSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.NeedApplierUI;

/// <summary>Presents the injury probability of the workshop (if any).</summary>
sealed class InjuryProbabilityFragment : IEntityPanelFragment {
  const float MaxProbabilityForColorLabel = 2;
  const string InjuryNeedId = "Injury";
  const string InjuryProbabilityLocKey = "IgorZ.TimberCommons.InjuryProbability";
  const string InjuryProbabilityDailyLocKey = "IgorZ.TimberCommons.InjuryProbabilityDaily";

  readonly UiFactory _uiFactory;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly InjuryProbabilitySettings _settings;
  readonly EffectProbabilityService _effectProbabilityService;

  VisualElement _root;
  Label _injuryProbabilityLabel;
  Label _injuryProbabilityAvatarHint;
  string _injuryProbabilityText;

  WorkshopRandomNeedApplier _needApplier;
  bool _indicatorAttached;

  InjuryProbabilityFragment(UiFactory uiFactory, ITooltipRegistrar tooltipRegistrar,
                            InjuryProbabilitySettings settings, EffectProbabilityService effectProbabilityService) {
    _uiFactory = uiFactory;
    _tooltipRegistrar = tooltipRegistrar;
    _settings = settings;
    _effectProbabilityService = effectProbabilityService;
  }

  /// <inheritdoc/>
  public VisualElement InitializeFragment() {
    _injuryProbabilityAvatarHint = new Label {
        text = "🟢",
        style = {
            alignSelf = Align.FlexEnd,
        },
    };
    _tooltipRegistrar.Register(_injuryProbabilityAvatarHint, () => _injuryProbabilityText);
    _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: false);

    _injuryProbabilityLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragment();
    _root.Add(_injuryProbabilityLabel);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _needApplier = entity.GetComponent<WorkshopRandomNeedApplier>();
    if (_needApplier == null) {
      return;
    }
    if (!_indicatorAttached) {
      AttachIndicator();
    }
    UpdateInjuryProbability();
  }

  /// <inheritdoc/>
  public void ClearFragment() {
    _root.ToggleDisplayStyle(visible: false);
    _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: false);
    _needApplier = null;
  }

  /// <inheritdoc/>
  public void UpdateFragment() {
  }

  void UpdateInjuryProbability() {
    var injuryEffect = _needApplier._workshopRandomNeedApplierSpec.Effects.FirstOrDefault(e => e.NeedId == InjuryNeedId);
    if (injuryEffect == null) {
      _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: false);
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    var probabilityPct = _effectProbabilityService.GetEffectProbability(injuryEffect, _needApplier.ProbabilityGroupId);
    Color color;
    switch (injuryEffect.Probability) {
      case EffectProbability.Low:
        color = Color.green;
        break;
      case EffectProbability.Medium:
        color = Color.yellow;
        break;
      case EffectProbability.High:
        color = Color.red;
        break;
      default:
        DebugEx.Warning("Unknown probability value: {0}. Falling back to approximation", injuryEffect.Probability);
        var redRatio = Mathf.Clamp01(probabilityPct / MaxProbabilityForColorLabel);
        var greenRatio = 1 - redRatio;
        var scale = 1 / (redRatio < greenRatio ? greenRatio : redRatio);
        color = new Color(redRatio * scale, greenRatio * scale, 0);
        break;
    }
    _injuryProbabilityAvatarHint.style.color = color;
    var pctLocKey = InjuryProbabilityLocKey;
    if (_settings.ShowDailyProbability.Value) {
      var dailyProbability = probabilityPct;
      for (var i = 1; i < 24; i++) {
        dailyProbability *= Mathf.Pow(1f + probabilityPct, i);
      }
      probabilityPct = dailyProbability;
      pctLocKey = InjuryProbabilityDailyLocKey;
    }
    var coloredText = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{probabilityPct:0.###%}</color>";
    _injuryProbabilityText = _uiFactory.T(pctLocKey, coloredText);
    _injuryProbabilityLabel.text = _injuryProbabilityText;

    _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: _settings.ShowAvatarHint.Value);
    _root.ToggleDisplayStyle(visible: _settings.ShowInFragment.Value);
  }

  void AttachIndicator() {
    var rootElement = _root;
    while (rootElement != null) {
      var avatarElement = rootElement.Q<VisualElement>("EntityAvatar");
      if (avatarElement != null) {
        avatarElement.Add(_injuryProbabilityAvatarHint);
        _indicatorAttached = true;
        break;
      }
      rootElement = rootElement.parent;
    }
    if (!_indicatorAttached) {
      DebugEx.Error("Failed to find EntityAvatar in entity panel");
    }
  }
}
