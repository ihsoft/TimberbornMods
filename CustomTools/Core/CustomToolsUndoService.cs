// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Timberborn.EntitySystem;
using Timberborn.EntityUndoSystem;
using Timberborn.SingletonSystem;
using Timberborn.UndoSystem;

namespace IgorZ.CustomTools.Core;

/// <summary>Tracks entities created by custom tools and allows undoing only those creations.</summary>
public sealed class CustomToolsUndoService(EventBus eventBus, UndoableEntityFactory undoableEntityFactory)
    : IPostLoadableSingleton {

  #region API

  /// <summary>Returns if there is a custom tool action to undo.</summary>
  public bool CanUndo => _undoStack.Count > 0;

  /// <summary>Starts collecting entity creations for one tool action.</summary>
  public void BeginCapture() {
    if (_captureDepth == 0) {
      _currentStack.Clear();
    }
    _captureDepth++;
  }

  /// <summary>Commits the captured entity creations as one undo action.</summary>
  public void CommitCapture() {
    if (_captureDepth == 0) {
      return;
    }
    _captureDepth--;
    if (_captureDepth == 0 && _currentStack.Count > 0) {
      _undoStack.Push([.._currentStack]);
      _currentStack.Clear();
    }
  }

  /// <summary>Discards the currently captured entity creations.</summary>
  public void AbortCapture() {
    if (_captureDepth == 0) {
      return;
    }
    _captureDepth--;
    if (_captureDepth == 0) {
      _currentStack.Clear();
    }
  }

  /// <summary>Undoes the most recent custom tool action.</summary>
  public void Undo() {
    if (_undoStack.Count == 0) {
      return;
    }
    var undoables = _undoStack.Pop();
    _processingUndo = true;
    try {
      for (var i = undoables.Count - 1; i >= 0; i--) {
        undoables[i].Undo();
      }
    } finally {
      _processingUndo = false;
    }
  }

  /// <summary>Clears all remembered custom tool actions.</summary>
  public void Clear() {
    _captureDepth = 0;
    _currentStack.Clear();
    _undoStack.Clear();
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    eventBus.Register(this);
  }

  #endregion

  #region Event handlers

  /// <summary>Captures entity creations while a custom placement action is active.</summary>
  [OnEvent]
  public void OnEntityCreated(EntityCreatedEvent entityCreatedEvent) {
    if (_captureDepth == 0 || _processingUndo) {
      return;
    }
    var entity = entityCreatedEvent.Entity;
    var undoableEntity = undoableEntityFactory.CreateUninitialized(entity);
    _currentStack.Add(new CreatedEntityUndoable(entity, undoableEntity));
  }

  #endregion

  #region Implementation

  int _captureDepth;
  bool _processingUndo;
  readonly List<IUndoable> _currentStack = [];
  readonly Stack<List<IUndoable>> _undoStack = [];

  sealed class CreatedEntityUndoable(EntityComponent entity, UndoableEntity undoableEntity) : IUndoable {
    public void Undo() {
      if (!entity) {
        return;
      }
      undoableEntity.InitializeUndoableState();
      undoableEntity.Delete();
    }

    public void Redo() {
      undoableEntity.Create();
    }
  }

  #endregion
}
