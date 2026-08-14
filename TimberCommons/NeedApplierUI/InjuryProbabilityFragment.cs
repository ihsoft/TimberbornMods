// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberCommons.NeedApplier;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
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
  const string InjuriesLocKey = "IgorZ.TimberCommons.Injuries";
  const string GameMiscStylePath = "UI/Views/Game/GameMiscStyle";
  static readonly Color InjuryHistoryColor = new Color32(255, 99, 71, 255);
  static readonly Color InactiveInjuryHistoryColor = new Color32(102, 86, 82, 255);

  readonly UiFactory _uiFactory;
  readonly VisualElementLoader _visualElementLoader;
  readonly IAssetLoader _assetLoader;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly EffectProbabilityService _effectProbabilityService;

  VisualElement _root;
  Label _injuryProbabilityLabel;
  Label _injuryProbabilityAvatarHint;
  VisualElement _injuryHistoryRoot;
  readonly List<VisualElement> _injuryHistoryBars = new();
  string _injuryProbabilityText;

  WorkshopRandomNeedApplier _needApplier;
  WorkshopInjuryStatistics _injuryStatistics;
  bool _indicatorAttached;
  string _displayedInjuryHistory;
  bool _displayedShowAvatarHint;
  bool _displayedShowDailyProbability;
  bool _displayedShowInFragment;
  bool _displayedShowInjuryStatistics;

  InjuryProbabilityFragment(
      UiFactory uiFactory, VisualElementLoader visualElementLoader, IAssetLoader assetLoader,
      ITooltipRegistrar tooltipRegistrar,
      EffectProbabilityService effectProbabilityService) {
    _uiFactory = uiFactory;
    _visualElementLoader = visualElementLoader;
    _assetLoader = assetLoader;
    _tooltipRegistrar = tooltipRegistrar;
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
    _tooltipRegistrar.Register(_injuryProbabilityAvatarHint, CreateAvatarTooltip);
    _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: false);

    _injuryProbabilityLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragment();
    _root.Add(_injuryProbabilityLabel);
    _injuryHistoryRoot = CreateInjuryHistory(_injuryHistoryBars, useTooltipColors: false);
    _tooltipRegistrar.Register(_injuryHistoryRoot, () => CreateInjuryStatisticsText());
    _root.Add(_injuryHistoryRoot);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _needApplier = entity.GetComponent<WorkshopRandomNeedApplier>();
    if (_needApplier == null) {
      return;
    }
    _injuryStatistics = entity.GetComponent<WorkshopInjuryStatistics>();
    _displayedInjuryHistory = null;
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
    _injuryStatistics = null;
  }

  /// <inheritdoc/>
  public void UpdateFragment() {
    if (_injuryStatistics != null
        && (InjuryProbabilitySettings.ShowAvatarHint != _displayedShowAvatarHint
            || InjuryProbabilitySettings.ShowDailyProbability != _displayedShowDailyProbability
            || InjuryProbabilitySettings.ShowInFragment != _displayedShowInFragment
            || InjuryProbabilitySettings.ShowInjuryStatistics != _displayedShowInjuryStatistics
            || InjuryProbabilitySettings.ShowInjuryStatistics
            && CreateInjuryHistorySnapshot() != _displayedInjuryHistory)) {
      UpdateInjuryProbability();
    }
  }

  void UpdateInjuryProbability() {
    var injuryEffect = _needApplier._workshopRandomNeedApplierSpec.Effects
        .FirstOrDefault(e => e.NeedId == InjuryNeedId);
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
    if (InjuryProbabilitySettings.ShowDailyProbability) {
      probabilityPct = InjuryProbabilityCalculator.CalculateDailyProbability(probabilityPct);
      pctLocKey = InjuryProbabilityDailyLocKey;
    }
    var coloredText = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{probabilityPct:0.###%}</color>";
    _displayedShowAvatarHint = InjuryProbabilitySettings.ShowAvatarHint;
    _displayedShowDailyProbability = InjuryProbabilitySettings.ShowDailyProbability;
    _displayedShowInFragment = InjuryProbabilitySettings.ShowInFragment;
    _displayedShowInjuryStatistics = InjuryProbabilitySettings.ShowInjuryStatistics;
    var injuryProbabilityText = _uiFactory.T(pctLocKey, coloredText);
    _injuryProbabilityText = injuryProbabilityText;
    if (_displayedShowInjuryStatistics) {
      _displayedInjuryHistory = CreateInjuryHistorySnapshot();
      UpdateInjuryHistory(_injuryHistoryBars);
    }
    _injuryProbabilityLabel.text = injuryProbabilityText;
    _injuryHistoryRoot.ToggleDisplayStyle(visible: _displayedShowInFragment && _displayedShowInjuryStatistics);

    _injuryProbabilityAvatarHint.ToggleDisplayStyle(visible: _displayedShowAvatarHint);
    _root.ToggleDisplayStyle(visible: _displayedShowInFragment);
  }

  TooltipContent CreateAvatarTooltip() {
    if (!_displayedShowInjuryStatistics) {
      return TooltipContent.Create(() => _injuryProbabilityText);
    }
    return TooltipContent.Create(CreateAvatarTooltipContent);
  }

  VisualElement CreateAvatarTooltipContent() {
    var root = new VisualElement();
    root.styleSheets.Add(_assetLoader.Load<StyleSheet>(GameMiscStylePath));
    root.Add(new Label(_injuryProbabilityText));
    root.Add(new Label(CreateInjuryStatisticsText()));
    var bars = new List<VisualElement>();
    root.Add(CreateInjuryHistory(bars, useTooltipColors: true));
    UpdateInjuryHistory(bars);
    return root;
  }

  VisualElement CreateInjuryHistory(List<VisualElement> injuryHistoryBars, bool useTooltipColors) {
    var root = new VisualElement {
        style = {
            flexDirection = FlexDirection.Row,
        },
    };
    for (var i = 0; i <= DailyInjuryCounter.HistoryDays; i++) {
      var loadRate = _visualElementLoader.LoadVisualElement("Game/AttractionLoadRate");
      var currentDayMarker = loadRate.Q<VisualElement>("CurrentHourMarker");
      currentDayMarker.style.unityBackgroundImageTintColor = InjuryHistoryColor;
      currentDayMarker.ToggleDisplayStyle(visible: i == 0);
      root.Add(loadRate);
      var injuryHistoryBar = loadRate.Q<VisualElement>("Rate");
      if (useTooltipColors) {
        injuryHistoryBar.parent.style.backgroundImage = StyleKeyword.None;
        injuryHistoryBar.parent.style.backgroundColor = InactiveInjuryHistoryColor;
      }
      injuryHistoryBar.style.backgroundImage = StyleKeyword.None;
      injuryHistoryBar.style.backgroundColor = InjuryHistoryColor;
      injuryHistoryBars.Add(injuryHistoryBar);
    }
    return root;
  }

  string CreateInjuryStatisticsText() {
    return _uiFactory.T(
        InjuriesLocKey, _injuryStatistics.InjuriesYesterday, _injuryStatistics.InjuriesInLastWeek);
  }

  string CreateInjuryHistorySnapshot() {
    return $"{_injuryStatistics.InjuriesToday},{string.Join(",", _injuryStatistics.InjuryHistory)}";
  }

  void UpdateInjuryHistory(List<VisualElement> injuryHistoryBars) {
    var history = _injuryStatistics.InjuryHistory;
    var maxInjuries = Mathf.Max(_injuryStatistics.InjuriesToday, history.DefaultIfEmpty().Max());
    for (var i = 0; i < injuryHistoryBars.Count; i++) {
      var historyIndex = history.Count - i;
      var injuries = i == 0
          ? _injuryStatistics.InjuriesToday
          : historyIndex >= 0 ? history[historyIndex] : 0;
      var height = maxInjuries == 0 ? 0 : 100f * injuries / maxInjuries;
      injuryHistoryBars[i].style.height = new StyleLength(Length.Percent(height));
    }
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
