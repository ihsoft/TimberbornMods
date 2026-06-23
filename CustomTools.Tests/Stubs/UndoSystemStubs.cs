namespace Timberborn.UndoSystem;

public interface IUndoable {
  void Undo();

  void Redo();
}
