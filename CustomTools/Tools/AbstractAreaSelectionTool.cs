// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Bindito.Core;
using Timberborn.AreaSelectionSystem;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.BlockSystem;
using Timberborn.BuilderPrioritySystem;
using Timberborn.InputSystem;
using UnityEngine;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable MemberCanBePrivate.Global

namespace IgorZ.CustomTools.Tools;

/// <summary>The base class to the selection tool implementations.</summary>
/// <remarks>
/// Implements the boilerplate of the selection logic. The descendants only need to react to the business logic related
/// events to customize the behavior.
/// </remarks>
/// <seealso cref="ObjectFilterExpression"/>
/// <seealso cref="OnObjectAction"/>
/// <seealso cref="OnHighlightChange"/>
/// <seealso cref="OnSelectionModeChange"/>
/// <seealso cref="SetColorSchema"/>
public abstract class AbstractAreaSelectionTool : AbstractCustomTool, IInputProcessor {

  #region Inhertable properties

  /// <summary>Shortcut to the <see cref="InputService"/>.</summary>
  protected InputService InputService { get; private set; }

  /// <summary>Indicates if selection mode has started (mouse clicks in the map).</summary>
  /// <value><c>true</c> if player clicks and holds LMB over a valid block object on the map.</value>
  /// <seealso cref="OnSelectionModeChange"/>
  protected bool SelectionModeActive {
    get => _selectionModeActive;
    private set {
      if (value == _selectionModeActive) {
        return;
      }
      OnSelectionModeChange(value);
      _selectionModeActive = value;
      if (!value) {
        SelectedObjects = null;
      }
    }
  }
  bool _selectionModeActive;

  /// <summary>Returns the block object that is currently highlighted.</summary>
  /// <value>The object under cursor or <c>null</c> if no block object can be detected.</value>
  /// <seealso cref="OnHighlightChange"/>
  protected BlockObject HighlightedBlockObject {
    get => _highlightedBlockObject;
    private set {
      if (value == _highlightedBlockObject) {
        return;
      }
      OnHighlightChange(value);
      _highlightedBlockObject = value;
    }
  }
  BlockObject _highlightedBlockObject;

  /// <summary>All currently selected objects.</summary>
  /// <remarks>It's <c>null</c> if selection mode is not active.</remarks>
  /// <seealso cref="SelectionModeActive"/>
  protected ReadOnlyCollection<BlockObject> SelectedObjects { get; private set; }

  /// <summary>The full path to the custom cursor resource name.</summary>
  /// <remarks>The mod must have a resource of type <c>CustomCursor</c> in the assets with this name.</remarks>
  /// <value><c>null</c> if no custom cursor needed.</value>
  protected abstract string CursorName { get; }

  #endregion

  #region API

  /// <summary>Tells if object should be accepted to the selection.</summary>
  /// <remarks>The accepted objects will be passed into the action callback.</remarks>
  /// <param name="blockObject">The object to check the expression for.</param>
  /// <seealso cref="OnObjectAction"/>
  protected abstract bool ObjectFilterExpression(BlockObject blockObject);

  /// <summary>A callback that is called on every selected object when the action is committed.</summary>
  /// <remarks>Only objects that match the <see cref="ObjectFilterExpression"/> will be passed.</remarks>
  /// <param name="blockObject">The object to apply action to.</param>
  /// <seealso cref="ObjectFilterExpression"/>
  protected abstract void OnObjectAction(BlockObject blockObject);

  /// <summary>A callback that is called when a new block object is about to be highlighted.</summary>
  /// <seealso cref="HighlightedBlockObject"/>
  protected virtual void OnHighlightChange(BlockObject newObject) {
  }
  /// <summary>A callback that is called when selection mode is about to change.</summary>
  /// <seealso cref="SelectionModeActive"/>
  protected virtual void OnSelectionModeChange(bool newMode) {
  }

  /// <summary>Sets colors for the tool selection action.</summary>
  /// <remarks>
  /// It can be called at any moment to change the colors when needed. However, if the colors are constant, then this
  /// method should be called by a descendant from <see cref="Initialize"/> before handing over to the base. 
  /// </remarks>
  /// <param name="highlightColor">Color of the matching object when hovering over in non-selecting mode.</param>
  /// <param name="actionColor">Color of the matching objects in the range selection mode.</param>
  /// <param name="tileColor">Color of the selected ground tile in the range selection mode.</param>
  /// <param name="sideColor">Color of the selection boundary border in the range selection mode.</param>
  protected void SetColorSchema(Color highlightColor, Color actionColor, Color tileColor, Color sideColor) {
    _highlightColor = highlightColor;
    _actionColor = actionColor;
    _tileColor = tileColor;
    _sideColor = sideColor;
    CreateDrawers();
  }

  #endregion

  #region Tool overrides

  /// <inheritdoc/>
  public override void Enter() {
    InputService.AddInputProcessor(this);
    if (CursorName != null) {
      _cursorService.SetCursor(CursorName);
    }
  }

  /// <inheritdoc/>
  public override void Exit() {
    _areaBlockObjectPicker.Reset();
    InputService.RemoveInputProcessor(this);
    if (CursorName != null) {
      _cursorService.ResetCursor();
    }
    ShowNoneCallback();
  }

  #endregion

  #region IInputProcessor implementation

  /// <inheritdoc/>
  public virtual bool ProcessInput() {
    return _areaBlockObjectPicker.PickBlockObjects<BuilderPrioritizable>(
        PreviewCallback, ActionCallback, ShowNoneCallback);
  }

  #endregion

  #region CustomTool overrides

  /// <inheritdoc/>
  protected override void Initialize() {
    base.Initialize();
    _areaBlockObjectPicker = _areaBlockObjectPickerFactory.CreatePickingUpwards();
    CreateDrawers();
  }

  #endregion

  #region Implementation

  BlockObjectSelectionDrawer _highlightSelectionDrawer;
  BlockObjectSelectionDrawer _actionSelectionDrawer;
  AreaBlockObjectPicker _areaBlockObjectPicker;
  CursorService _cursorService;

  AreaBlockObjectPickerFactory _areaBlockObjectPickerFactory;
  BlockObjectSelectionDrawerFactory _blockObjectSelectionDrawerFactory;

  Color _highlightColor = Color.blue;
  Color _actionColor = Color.red;
  Color _tileColor = Color.blue;
  Color _sideColor = Color.blue;

  /// <summary>Injects the condition dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(AreaBlockObjectPickerFactory areaBlockObjectPickerFactory, InputService inputService,
                                 BlockObjectSelectionDrawerFactory blockObjectSelectionDrawerFactory,
                                 CursorService cursorService) {
    _areaBlockObjectPickerFactory = areaBlockObjectPickerFactory;
    InputService = inputService;
    _blockObjectSelectionDrawerFactory = blockObjectSelectionDrawerFactory;
    _cursorService = cursorService;
  }

  void PreviewCallback(IEnumerable<BlockObject> blockObjects, Vector3Int start, Vector3Int end,
                       bool selectionStarted, bool selectingArea) {
    var objects = blockObjects.ToList();
    if (selectionStarted) {
      SelectedObjects = objects.AsReadOnly();
    }
    HighlightedBlockObject = !selectingArea ? objects.FirstOrDefault() : objects.LastOrDefault();
    SelectionModeActive = selectionStarted;
    var targetObjects = objects.Where(ObjectFilterExpression);
    if (selectionStarted) {
      _actionSelectionDrawer.Draw(targetObjects, start, end, selectingArea);
    } else {
      _highlightSelectionDrawer.Draw(targetObjects, start, end, selectingArea: false);
    }
  }

  void ActionCallback(IEnumerable<BlockObject> blockObjects, Vector3Int start, Vector3Int end,
                      bool selectionStarted, bool selectingArea) {
    blockObjects
        .Where(ObjectFilterExpression)
        .ToList()
        .ForEach(OnObjectAction);
    CancelSelectionMode();
  }

  void ShowNoneCallback() {
    CancelSelectionMode();
  }

  void CancelSelectionMode() {
    _highlightSelectionDrawer.StopDrawing();
    _actionSelectionDrawer.StopDrawing();
    SelectionModeActive = false;
    HighlightedBlockObject = null;
  }

  void CreateDrawers() {
    _highlightSelectionDrawer = _blockObjectSelectionDrawerFactory.Create(_highlightColor, _tileColor, _sideColor);
    _actionSelectionDrawer = _blockObjectSelectionDrawerFactory.Create(_actionColor, _tileColor, _sideColor);
  }

  #endregion
}
