// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.Settings;
using IgorZ.TimberDev.UI;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace IgorZ.Automation.SettingsUI;

sealed class SignalModuleOrderDialog(
    ScriptingService scriptingService, ITooltipRegistrar tooltipRegistrar) : AbstractDialog {

  const string DialogAsset = "IgorZ.Automation/SignalModuleOrderDialog";
  const string RowTmplAsset = "IgorZ.Automation/SignalModuleOrderRowTmpl";

  const string MoveUpLocKey = "IgorZ.Automation.SignalModuleOrder.MoveUp";
  const string MoveDownLocKey = "IgorZ.Automation.SignalModuleOrder.MoveDown";

  #region AbstractDialog implementation

  /// <inheritdoc />
  protected override string DialogResourceName => DialogAsset;

  /// <inheritdoc />
  protected override string VerifyInput() => null;

  /// <inheritdoc />
  protected override void ApplyInput() {
    var moduleOrder = _moduleNames.ToArray();
    if (moduleOrder.SequenceEqual(_registeredModuleOrder)) {
      ScriptEditorSettings.ResetSignalModuleOrder();
    } else {
      ScriptEditorSettings.SetSignalModuleOrder(moduleOrder);
    }
  }

  /// <inheritdoc />
  protected override bool CheckHasChanges() {
    return !_moduleNames.SequenceEqual(_initialModuleOrder);
  }

  #endregion

  #region API

  /// <inheritdoc />
  public override void Show() {
    base.Show();

    _moduleList = Root.Q2<ListView>("ModuleList");
    // ListView cannot serialize itemsSource, so UI Builder uses a separate authoring-time preview.
    Root.Q2<ScrollView>("ModuleListPreview").RemoveFromHierarchy();
    _registeredModuleOrder = scriptingService.GetScriptableNames().ToArray();
    _initialModuleOrder = SignalModuleOrder.Apply(_registeredModuleOrder, moduleName => moduleName).ToArray();
    SetModuleOrder(_initialModuleOrder);

    _moduleList.makeItem = MakeModuleRow;
    _moduleList.bindItem = BindModuleRow;
    _moduleList.itemsSource = _moduleNames;
    _moduleList.itemIndexChanged += OnItemIndexChanged;
    Root.Q2<Button>("ResetButton").clicked += ResetOrder;
  }

  /// <inheritdoc />
  public override void Close() {
    if (_moduleList != null) {
      _moduleList.itemIndexChanged -= OnItemIndexChanged;
      _moduleList.itemsSource = null;
    }
    _moduleList = null;
    _moduleNames.Clear();
    _registeredModuleOrder = [];
    _initialModuleOrder = [];
    base.Close();
  }

  #endregion

  #region Implementation

  readonly List<string> _moduleNames = [];
  ListView _moduleList;
  string[] _registeredModuleOrder = [];
  string[] _initialModuleOrder = [];

  sealed class ModuleRowState(VisualElement root) {
    public Label Label { get; } = root.Q2<Label>("ModuleName");
    public Button MoveUpButton { get; } = root.Q2<Button>("MoveUpButton");
    public Button MoveDownButton { get; } = root.Q2<Button>("MoveDownButton");
    public int Index { get; set; }
  }

  VisualElement MakeModuleRow() {
    var root = UiFactory.LoadVisualElement(RowTmplAsset, unwrapContainer: true);
    var rowState = new ModuleRowState(root);
    root.userData = rowState;
    tooltipRegistrar.RegisterLocalizable(rowState.MoveUpButton, MoveUpLocKey);
    tooltipRegistrar.RegisterLocalizable(rowState.MoveDownButton, MoveDownLocKey);
    rowState.MoveUpButton.clicked += () => MoveModule(rowState.Index, -1);
    rowState.MoveDownButton.clicked += () => MoveModule(rowState.Index, 1);
    return root;
  }

  void BindModuleRow(VisualElement root, int index) {
    var rowState = (ModuleRowState)root.userData;
    rowState.Index = index;
    rowState.Label.text = _moduleNames[index];
    rowState.MoveUpButton.SetEnabled(index > 0);
    rowState.MoveDownButton.SetEnabled(index < _moduleNames.Count - 1);
  }

  void MoveModule(int index, int delta) {
    var newIndex = index + delta;
    if (index < 0 || newIndex < 0 || newIndex >= _moduleNames.Count) {
      return;
    }

    (_moduleNames[index], _moduleNames[newIndex]) = (_moduleNames[newIndex], _moduleNames[index]);
    _moduleList.RefreshItems();
    _moduleList.ScrollToItem(newIndex);
  }

  void OnItemIndexChanged(int _, int newIndex) {
    _moduleList.ScrollToItem(newIndex);
  }

  void ResetOrder() {
    SetModuleOrder(_registeredModuleOrder);
    _moduleList.Rebuild();
    _moduleList.ScrollToItem(0);
  }

  void SetModuleOrder(IEnumerable<string> moduleNames) {
    _moduleNames.Clear();
    _moduleNames.AddRange(moduleNames);
  }

  #endregion
}
