namespace Timberborn.BlockingSystem;

public sealed class BlockableObject {
  public bool IsBlocked { get; private set; }
  public object Blocker { get; private set; }

  public bool IsUnblocked => !IsBlocked;

  public void Block(object blocker) {
    IsBlocked = true;
    Blocker = blocker;
  }

  public void Unblock(object blocker) {
    if (Blocker == blocker) {
      IsBlocked = false;
      Blocker = null;
    }
  }
}
