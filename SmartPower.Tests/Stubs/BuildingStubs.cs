using System;

namespace Timberborn.Buildings;

public sealed class PausableBuilding {
  public bool Paused { get; set; }

  public event EventHandler PausedChanged;

  public void RaisePausedChanged() {
    PausedChanged?.Invoke(this, EventArgs.Empty);
  }
}

public interface IFinishedStateListener {
  void OnEnterFinishedState();
  void OnExitFinishedState();
}
