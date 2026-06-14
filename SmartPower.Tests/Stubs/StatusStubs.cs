namespace Timberborn.StatusSystem;

public sealed class StatusToggle {
  public bool Active { get; private set; }

  public static StatusToggle CreateNormalStatus(string icon, string text) {
    return new StatusToggle();
  }

  public static StatusToggle CreateNormalStatusWithFloatingIcon(string icon, string text) {
    return new StatusToggle();
  }

  public void Activate() {
    Active = true;
  }

  public void Deactivate() {
    Active = false;
  }
}

public sealed class StatusSubject {
  public void RegisterStatus(StatusToggle status) {
  }
}
