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
using IgorZ.TimberDev.Utils;
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
  const string CreateManualSignalValue = "<create-manual-signal>";

  const string CreateManualSignalLocKey = "IgorZ.Automation.PinnedCustomSignals.CreateManualSignal";
  const string AttachSignalLocKey = "IgorZ.Automation.PinnedCustomSignals.AttachSignal";
  const string NoSignalsLocKey = "IgorZ.Automation.PinnedCustomSignals.NoSignals";
  const string NoMoreSignalsLocKey = "IgorZ.Automation.PinnedCustomSignals.NoMoreSignals";
  const string SignalValueLocKey = "IgorZ.Automation.PinnedCustomSignals.SignalValue";
  const string SetSignalValueLocKey = "IgorZ.Automation.PinnedCustomSignals.SetSignalValue";
  const string DeleteManualSignalLocKey = "IgorZ.Automation.PinnedCustomSignals.DeleteManualSignal";
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
  readonly Dictionary<string, VisualElement> _signalRows = [];
  readonly Dictionary<string, Label> _signalLabels = [];
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
    _signalDispatcher.SignalValuesChanged += OnSignalValuesChanged;
    ScriptEngineSettings.PinnedCustomSignalsChanged += OnPinnedCustomSignalsSettingChanged;
    _eventBus.Register(this);
    CreatePinnedSignalRows();
    UpdateSignalSelector();
    UpdatePanelVisibility();
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
    if (ScriptEngineSettings.PinnedCustomSignals) {
      UpdateSignalSelector();
    }
  }

  void OnSignalValuesChanged(object sender, EventArgs e) {
    if (_signalLabels.Count == 0) {
      return;
    }
    UpdateSignalValues();
  }

  void OnPinnedCustomSignalsSettingChanged(object sender, EventArgs e) {
    if (ScriptEngineSettings.PinnedCustomSignals) {
      UpdateSignalSelector();
    }
    UpdatePanelVisibility();
  }

  void AttachSignal(string signalName) {
    if (signalName == CreateManualSignalValue) {
      ShowCreateManualSignalDialog();
      _signalSelector.SelectedValue = "";
      return;
    }
    if (string.IsNullOrEmpty(signalName) || _pinnedSignals.Contains(signalName)) {
      return;
    }
    _pinnedSignals.Add(signalName);
    CreatePinnedSignalRow(signalName);
    UpdateSignalSelector();
  }

  void UnpinSignal(string signalName) {
    RemovePinnedSignal(signalName);
    UpdateSignalSelector();
  }

  void DeleteManualSignal(string signalName) {
    RemovePinnedSignal(signalName);
    UpdateSignalSelector();
    _signalDispatcher.UnsetManualSignalValue(signalName);
  }

  void CreatePinnedSignalRows() {
    foreach (var signalName in _pinnedSignals) {
      CreatePinnedSignalRow(signalName);
    }
  }

  void UpdatePanelVisibility() {
    _root.ToggleDisplayStyle(ScriptEngineSettings.PinnedCustomSignals);
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
    }
        .Concat(unpinnedSignals.Select(x => new DropdownItem { Value = x, Text = FormatSignalName(x) }))
        .Append(new DropdownItem { Value = CreateManualSignalValue, Text = _uiFactory.T(CreateManualSignalLocKey) })
        .ToArray();
    _signalSelector.SetEnabled(true);
  }

  void CreatePinnedSignalRow(string signalName) {
    if (_signalRows.ContainsKey(signalName)) {
      return;
    }
    var row = _uiFactory.LoadVisualElement(PinResource);
    row.Q2<Image>("StateIcon").ToggleDisplayStyle(visible: false);
    var signalLabel = row.Q2<Label>("Name");
    signalLabel.text = _uiFactory.T(
        SignalValueLocKey, FormatSignalName(signalName), GetFormattedSignalValue(signalName));
    _signalLabels[signalName] = signalLabel;
    signalLabel.RegisterCallback<ClickEvent>(_ => ShowSetSignalValueDialog(signalName));
    _tooltipRegistrar.RegisterLocalizable(signalLabel, SetSignalValueLocKey);

    var hasManualValue = _signalDispatcher.HasManualSignalValue(signalName);
    var removeButton = new Button();
    removeButton.AddToClassList("button-square");
    removeButton.AddToClassList("button-square--small");
    removeButton.AddToClassList(hasManualValue ? "button-cross" : "button-minus");
    removeButton.style.marginRight = 3;
    removeButton.clicked += () => {
      if (hasManualValue) {
        DeleteManualSignal(signalName);
      } else {
        UnpinSignal(signalName);
      }
    };
    _tooltipRegistrar.RegisterLocalizable(
        removeButton, hasManualValue ? DeleteManualSignalLocKey : UnpinSignalLocKey);
    row.Insert(0, removeButton);

    _signalRows[signalName] = row;
    _signalsContainer.Add(row);
  }

  void RemovePinnedSignal(string signalName) {
    _pinnedSignals.Remove(signalName);
    _signalLabels.Remove(signalName);
    if (!_signalRows.Remove(signalName, out var row)) {
      return;
    }
    row.RemoveFromHierarchy();
  }

  void UpdateSignalValues() {
    foreach (var (signalName, signalLabel) in _signalLabels) {
      signalLabel.text = _uiFactory.T(
          SignalValueLocKey, FormatSignalName(signalName), GetFormattedSignalValue(signalName));
    }
  }

  void ShowSetSignalValueDialog(string signalName) {
    StaticBindings.DependencyContainer.GetInstance<SetCustomSignalValueDialog>()
        .WithSignal(signalName, FormatSignalName(signalName), _signalDispatcher.GetSignalValue(signalName))
        .Show();
  }

  void ShowCreateManualSignalDialog() {
    StaticBindings.DependencyContainer.GetInstance<SetCustomSignalValueDialog>()
        .WithNewSignal(GetCustomSignalNames(), signalName => {
          if (!_pinnedSignals.Contains(signalName)) {
            _pinnedSignals.Add(signalName);
            CreatePinnedSignalRow(signalName);
            UpdateSignalSelector();
          }
        })
        .Show();
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
