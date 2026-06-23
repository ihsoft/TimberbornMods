using System.Collections.Generic;
using Timberborn.EntitySystem;

namespace Timberborn.EntityUndoSystem;

public class UndoableEntityFactory {
  readonly Dictionary<EntityComponent, UndoableEntity> _undoables = new();

  public int CreateUninitializedCalls { get; private set; }

  public UndoableEntity CreateUninitialized(EntityComponent entity) {
    CreateUninitializedCalls++;
    if (!_undoables.TryGetValue(entity, out var undoableEntity)) {
      undoableEntity = new UndoableEntity();
      _undoables[entity] = undoableEntity;
    }
    return undoableEntity;
  }

  public UndoableEntity GetUndoable(EntityComponent entity) {
    return _undoables[entity];
  }
}

public class UndoableEntity {
  static int _nextOperationId;

  public int DeleteCalls { get; private set; }

  public int InitializeUndoableStateCalls { get; private set; }

  int _lastDeleteOperationId;

  public void InitializeUndoableState() {
    InitializeUndoableStateCalls++;
  }

  public void Delete() {
    DeleteCalls++;
    _lastDeleteOperationId = ++_nextOperationId;
  }

  public void Create() {
  }

  public bool DeletedBefore(UndoableEntity other) {
    return _lastDeleteOperationId > 0 && _lastDeleteOperationId < other._lastDeleteOperationId;
  }
}
