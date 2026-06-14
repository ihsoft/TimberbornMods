// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.CustomTools.Core;
using IgorZ.CustomTools.KeyBindings;
using Timberborn.AreaSelectionSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionGuidelines;
using Timberborn.ConstructionMode;
using Timberborn.Coordinates;
using Timberborn.Debugging;
using Timberborn.EntitySystem;
using Timberborn.GameFactionSystem;
using Timberborn.InputSystem;
using Timberborn.ScienceSystem;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using Timberborn.ToolSystem;
using Timberborn.UISound;

namespace IgorZ.CustomTools.Tools;

/// <summary>The base class to the BlockObject tools that can place different templates.</summary>
/// <remarks>
/// This tool also supports the Undo ability. The block objects placed by the tool <i>before</i> existing it
/// can be undone. The undo key binding is constant: <see cref="UndoPlacementsKeyBinding"/>. Due to the tool description
/// is not a dynamic thing, the primary binding is fixed to "Ctrl+Z". This is used in the tool description. A secondary
/// binding can be defined, but the primary one cannot be changed.
/// </remarks>
/// <typeparam name="T">the mode control that defines which template will be used to place the object.</typeparam>
public abstract class AbstractMultiTemplateBlockObjectTool<T> 
    : AbstractCustomTool, IInputProcessor, IConstructionModeEnabler, IBlockObjectGridTool where T: Enum {

  const string BlockObjectPlacedSoundName = "UI.BlockObjectPlaced";
  const string UndoPlacementsKeyBinding = "IgorZ-CustomTools-Undo";
  const string UndoHintLocKey = "IgorZ.CustomTools.BlockObjectTool.UndoHint";
  const string TemplateIsUnlockedLocKey = "IgorZ.CustomTools.MultiTemplateBlockObjectTool.TemplateIsUnlocked";

  #region API

  /// <summary>The currently selected template.</summary>
  protected PlaceableBlockObjectSpec Template { get; private set; }

  /// <summary>Returns the placeable spec for the mode.</summary>
  protected abstract PlaceableBlockObjectSpec GetTemplateForMode(T mode);

  /// <summary>Returns the mode in which the tool needs to run in.</summary>
  /// <remarks>
  /// This is a high frequency method. It is called every video frame. As long as the mode doesn't change between the
  /// frames, the logic is cheap. If the mode is changed, then there will be stuff updated.
  /// </remarks>
  /// <seealso cref="GetTemplateForMode"/>
  protected abstract T SelectMode();

  /// <summary>Tells if the currently selected template wa researched and unblocked for building.</summary>
  protected bool IsTemplateUnlocked =>
      _devModeManager.Enabled || _buildingUnlockingService.Unlocked(Template.GetSpec<BuildingSpec>());

  /// <summary>The current mode of this tool.</summary>
  /// <remarks>The descendants must set the initial state from <see cref="Initialize"/> method.</remarks>
  protected T CurrentMode {
    get => _currentMode;
    set {
      if (value.Equals(_currentMode) && _previewPlacer != null) {
        return;
      }
      _currentMode = value;
      _previewPlacer?.HideAllPreviews();
      Template = GetTemplateForMode(_currentMode);
      _previewPlacer = _previewPlacerFactory.Create(Template);
      OnModeUpdated();
    }
  }
  T _currentMode;

  /// <summary>The callback that is called when the mode has changed.</summary>
  protected virtual void OnModeUpdated() {}

  /// <summary>Returns a localized display name string for the template.</summary>
  protected string GetTemplateDisplayName(ComponentSpec template) {
    return Loc.T(template.GetSpec<LabeledEntitySpec>().DisplayNameLocKey);
  }

  /// <summary>A shortcut to the templates service.</summary>
  /// <param name="name">The full name of the template.</param>
  protected PlaceableBlockObjectSpec GetTemplate(string name) {
    return _templateNameMapper.GetTemplate(name).GetSpec<PlaceableBlockObjectSpec>();
  }

  /// <summary>A shortcut to the templates service, but without requiring the faction suffix.</summary>
  /// <remarks>
  /// This method will do two lookups: for the name "as-is" and for the name with the current faction ID as suffix. Use
  /// it when the name is the same for the factions, and you don't want to create separate setups.
  /// </remarks>
  /// <param name="name">The full name of the template with or <i>without</i> the faction ID suffix.</param>
  protected PlaceableBlockObjectSpec GetTemplateNoFaction(string name) {
    if (_templateNameMapper.TryGetTemplate(name, out var template)) {
      return template.GetSpec<PlaceableBlockObjectSpec>();
    }
    name += $".{_factionService.Current.Id}";
    return _templateNameMapper.GetTemplate(name).GetSpec<PlaceableBlockObjectSpec>();
  }

  #endregion

  #region IInputProcessor implementation

  /// <inheritdoc/>
  public virtual bool ProcessInput() {
    CurrentMode = SelectMode();
    return _areaPicker.PickBlockObjectArea(
        Template, _previewPlacement.Orientation, _previewPlacement.FlipMode, PreviewCallback, ActionCallback);
  }

  #endregion

  #region AbstractCustomTool implementation

  /// <inheritdoc/>
  protected override void Initialize() {
    base.Initialize();
    DescriptionBullets = DescriptionBullets == null
        ? [Loc.T(UndoHintLocKey)]
        : DescriptionBullets.Concat([Loc.T(UndoHintLocKey)]).ToArray();
  }

  /// <inheritdoc/>
  public override string GetWarningText() {
    return !IsTemplateUnlocked
        ? Loc.T(TemplateIsUnlockedLocKey, GetTemplateDisplayName(Template))
        : _previewPlacer.WarningText;
  }

  /// <inheritdoc/>
  public override void Enter() {
    _defaultMode = CurrentMode;
    _inputService.AddInputProcessor(this);
    _eventBus.Register(this);
  }
  T _defaultMode;

  /// <inheritdoc/>
  public override void Exit() {
    _inputService.RemoveInputProcessor(this);
    _previewPlacer.HideAllPreviews();
    _areaPicker.Reset();
    _placedAnythingThisFrame = false;
    _customToolsUndoService.Clear();
    _eventBus.Unregister(this);
    CurrentMode = _defaultMode;
  }

  #endregion

  #region Implementation

  InputService _inputService;
  TemplateNameMapper _templateNameMapper;
  PreviewPlacerFactory _previewPlacerFactory;
  BlockObjectPlacerService _blockObjectPlacerService;
  UISoundController _uiSoundController;
  PreviewPlacement _previewPlacement;
  AreaPicker _areaPicker;
  FactionService _factionService;
  EventBus _eventBus;
  KeyBindingInputProcessor _keyBindingInputProcessor;
  CustomToolsService _customToolsService;
  CustomToolsUndoService _customToolsUndoService;
  ToolUnlockingService _toolUnlockingService;
  BuildingUnlockingService _buildingUnlockingService;
  DevModeManager _devModeManager;

  PreviewPlacer _previewPlacer;
  bool _placedAnythingThisFrame;

  /// <summary>Has to be public for the injection to work!</summary>
  [Inject]
  public void InjectDependencies(
      InputService inputService, TemplateNameMapper templateNameMapper,
      PreviewPlacerFactory previewPlacerFactory, BlockObjectPlacerService blockObjectPlacerService,
      AreaPicker areaPicker, UISoundController uiSoundController, PreviewPlacement previewPlacement,
      FactionService factionService, EventBus eventBus, KeyBindingInputProcessor keyBindingInputProcessor,
      CustomToolsService customToolsService, CustomToolsUndoService customToolsUndoService,
      ToolUnlockingService toolUnlockingService,
      BuildingUnlockingService buildingUnlockingService, DevModeManager devModeManager) {
    _inputService = inputService;
    _templateNameMapper = templateNameMapper;
    _previewPlacerFactory = previewPlacerFactory;
    _blockObjectPlacerService = blockObjectPlacerService;
    _areaPicker = areaPicker;
    _uiSoundController = uiSoundController;
    _previewPlacement = previewPlacement;
    _factionService = factionService;
    _eventBus = eventBus;
    _keyBindingInputProcessor = keyBindingInputProcessor;
    _customToolsService = customToolsService;
    _customToolsUndoService = customToolsUndoService;
    _toolUnlockingService = toolUnlockingService;
    _buildingUnlockingService = buildingUnlockingService;
    _devModeManager = devModeManager;
  }

  void PreviewCallback(IEnumerable<Placement> placements) {
    if (_placedAnythingThisFrame) { 
      _placedAnythingThisFrame = false; 
    } else {
      _previewPlacer.ShowPreviews(placements);
    }
  }

  void ActionCallback(IEnumerable<Placement> placements) {
    var blockObjectTool = _customToolsService.BlockObjectTools.GetValueOrDefault(Template.Blueprint.Name);
    if (IsTemplateUnlocked || blockObjectTool == null) {
      Place(placements);
      return;
    }
    if (_toolUnlockingService.IsLocked(blockObjectTool)) {
      _toolUnlockingService.TryToUnlock(blockObjectTool, () => Place(placements), _previewPlacer.HideAllPreviews);
    } else {
      Place(placements);
    } 
  }

  void Place(IEnumerable<Placement> placements) {
    if (!IsTemplateUnlocked) {
      _uiSoundController.PlayCantDoSound();
      return;
    }
    _placedAnythingThisFrame = false;
    var buildableCoordinates = _previewPlacer.GetBuildableCoordinates(placements);
    var blockObjectPlacer = _blockObjectPlacerService.GetMatchingPlacer(Template.GetSpec<BlockObjectSpec>());
    var committedCapture = false;
    _customToolsUndoService.BeginCapture();
    try {
      foreach (var placement in buildableCoordinates) {
        var builder = new EntitySetup.Builder(Template.Blueprint);
        blockObjectPlacer.Place(builder, placement);
        _placedAnythingThisFrame = true;
      }
      _customToolsUndoService.CommitCapture();
      committedCapture = true;
    } finally {
      if (!committedCapture) {
        _customToolsUndoService.AbortCapture();
      }
    }
    if (_placedAnythingThisFrame) {
      _uiSoundController.PlaySound(BlockObjectPlacedSoundName);
    } else {
      _uiSoundController.PlayCantDoSound();
    }
  }

  /// <summary>Listens for the undo keybinding.</summary>
  [OnEvent]
  public void OnCustomToolEvent(CustomToolKeyBindingEvent keyBindingEvent) {
    if (!_customToolsUndoService.CanUndo || keyBindingEvent.KeyBinding.Id != UndoPlacementsKeyBinding) {
      return;
    }
    _keyBindingInputProcessor.ConsumeKeyBinding(keyBindingEvent.KeyBinding.Id);
    _customToolsUndoService.Undo();
  }

  #endregion
}
