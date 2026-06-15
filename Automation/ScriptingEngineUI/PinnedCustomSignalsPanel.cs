// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Settings;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using Timberborn.WorldPersistence;
using UnityEngine.UIElements;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class PinnedCustomSignalsPanel : ILoadableSingleton, IPostLoadableSingleton, ISaveableSingleton {

  const string PanelResource = "Game/AutomationPins/PinnedIndicatorsPanel";
  const string PinResource = "Game/AutomationPins/PinnedIndicator";
  const string SignalNamePrefix = "Signals.";

  const string AttachSignalLocKey = "IgorZ.Automation.PinnedCustomSignals.AttachSignal";
  const string NoSignalsLocKey = "IgorZ.Automation.PinnedCustomSignals.NoSignals";
  const string NoMoreSignalsLocKey = "IgorZ.Automation.PinnedCustomSignals.NoMoreSignals";
  const string SignalValueLocKey = "IgorZ.Automation.PinnedCustomSignals.SignalValue";
  const string UnpinSignalLocKey = "IgorZ.Automation.PinnedCustomSignals.UnpinSignal";

  static readonly SingletonKey PinnedCustomSignalsKey = new("IgorZ.Automation.PinnedCustomSignalsPanel");
  static readonly ListKey<string> PinnedSignalsKey = new("PinnedSignals");

  readonly UiFactory _uiFactory;
  readonly UILayout _uiLayout;
  readonly EventBus _eventBus;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly ISingletonLoader _singletonLoader;
  readonly SignalDispatcher _signalDispatcher;

  readonly List<string> _pinnedSignals = [];
  VisualElement _root;
  VisualElement _signalsContainer;
  ResizableDropdownElement _signalSelector;
  bool _isAddedToLayout;

  PinnedCustomSignalsPanel(
      UiFactory uiFactory, UILayout uiLayout, EventBus eventBus, ITooltipRegistrar tooltipRegistrar,
      ISingletonLoader singletonLoader, SignalDispatcher signalDispatcher) {
    _uiFactory = uiFactory;
    _uiLayout = uiLayout;
    _eventBus = eventBus;
    _tooltipRegistrar = tooltipRegistrar;
    _singletonLoader = singletonLoader;
    _signalDispatcher = signalDispatcher;
  }

  /// <inheritdoc/>
  public void Load() {
    if (!_singletonLoader.TryGetSingleton(PinnedCustomSignalsKey, out var objectLoader)) {
      return;
    }
    _pinnedSignals.Clear();
    _pinnedSignals.AddRange(objectLoader.Get(PinnedSignalsKey).Distinct());
  }

  /// <inheritdoc/>
  public void PostLoad() {
    _root = _uiFactory.LoadVisualElement(PanelResource);
    _signalsContainer = _root.Q2<VisualElement>("Indicators");
    _signalSelector = _uiFactory.CreateSimpleDropdown(AttachSignal);
    _signalSelector.AutoResizeToOptions = false;
    _signalSelector.style.width = 190;
    _signalSelector.style.marginBottom = 2;
    _root.Insert(0, _signalSelector);

    _signalDispatcher.SignalsChanged += OnSignalsChanged;
    ScriptEngineSettings.PinnedCustomSignalsChanged += OnPinnedCustomSignalsSettingChanged;
    _eventBus.Register(this);
    Recreate();
  }

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) {
    singletonSaver.GetSingleton(PinnedCustomSignalsKey).Set(PinnedSignalsKey, _pinnedSignals);
  }

  [OnEvent]
  public void OnShowPrimaryUI(ShowPrimaryUIEvent showPrimaryUIEvent) {
    if (_isAddedToLayout) {
      return;
    }
    _uiLayout.AddTopLeft(_root, 41);
    _isAddedToLayout = true;
  }

  void OnSignalsChanged(object sender, EventArgs e) {
    Recreate();
  }

  void OnPinnedCustomSignalsSettingChanged(object sender, EventArgs e) {
    Recreate();
  }

  void AttachSignal(string signalName) {
    if (string.IsNullOrEmpty(signalName) || _pinnedSignals.Contains(signalName)) {
      return;
    }
    _pinnedSignals.Add(signalName);
    Recreate();
  }

  void UnpinSignal(string signalName) {
    _pinnedSignals.Remove(signalName);
    Recreate();
  }

  void Recreate() {
    if (!ScriptEngineSettings.PinnedCustomSignals) {
      _root.ToggleDisplayStyle(visible: false);
      return;
    }
    UpdateSignalSelector();
    _signalsContainer.Clear();
    foreach (var signalName in _pinnedSignals.ToList()) {
      CreatePinnedSignalRow(signalName);
    }
    _root.ToggleDisplayStyle(_pinnedSignals.Count > 0 || GetCustomSignalNames().Count > 0);
  }

  void UpdateSignalSelector() {
    var customSignalNames = GetCustomSignalNames();
    var unpinnedSignals = customSignalNames
        .Where(x => !_pinnedSignals.Contains(x))
        .ToList();
    string placeholder;
    if (customSignalNames.Count == 0) {
      placeholder = _uiFactory.T(NoSignalsLocKey);
    } else {
      placeholder = unpinnedSignals.Count == 0 ? _uiFactory.T(NoMoreSignalsLocKey) : _uiFactory.T(AttachSignalLocKey);
    }
    _signalSelector.Items = new[] {
        new DropdownItem { Value = "", Text = placeholder },
    }.Concat(unpinnedSignals.Select(x => new DropdownItem { Value = x, Text = FormatSignalName(x) })).ToArray();
    _signalSelector.SetEnabled(unpinnedSignals.Count > 0);
  }

  void CreatePinnedSignalRow(string signalName) {
    var row = _uiFactory.LoadVisualElement(PinResource);
    row.Q2<Image>("StateIcon").ToggleDisplayStyle(visible: false);
    row.Q2<Label>("Name").text = _uiFactory.T(
        SignalValueLocKey, FormatSignalName(signalName), GetFormattedSignalValue(signalName));

    var unpinButton = new Button();
    unpinButton.AddToClassList("button-square");
    unpinButton.AddToClassList("button-square--small");
    unpinButton.AddToClassList("button-minus");
    unpinButton.style.marginRight = 3;
    unpinButton.clicked += () => UnpinSignal(signalName);
    _tooltipRegistrar.RegisterLocalizable(unpinButton, UnpinSignalLocKey);
    row.Insert(0, unpinButton);

    _signalsContainer.Add(row);
  }

  List<string> GetCustomSignalNames() {
    return _signalDispatcher.GetRegisteredSignals()
        .Where(x => x.StartsWith(SignalNamePrefix))
        .OrderBy(x => x)
        .ToList();
  }

  string GetFormattedSignalValue(string signalName) {
    return ScriptValue.Of(_signalDispatcher.GetSignalValue(signalName)).AsFloat.ToString("0.##");
  }

  static string FormatSignalName(string signalName) {
    return signalName.StartsWith(SignalNamePrefix)
        ? signalName[SignalNamePrefix.Length..]
        : signalName;
  }
}
