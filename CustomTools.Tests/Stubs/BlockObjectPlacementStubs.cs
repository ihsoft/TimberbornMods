namespace Timberborn.BlockObjectTools;

public class PreviewPlacement {
  public bool FlippingEnabled { get; private set; }

  public void EnableFlipping() {
    FlippingEnabled = true;
  }

  public void DisableFlipping() {
    FlippingEnabled = false;
  }
}
