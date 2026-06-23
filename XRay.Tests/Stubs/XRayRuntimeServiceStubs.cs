namespace IgorZ.XRay.Core;

class TransparentTerrainMeshService {
  public int ActivateCalls { get; private set; }

  public int DeactivateCalls { get; private set; }

  public void Activate() {
    ActivateCalls++;
  }

  public void Deactivate() {
    DeactivateCalls++;
  }
}

class WireframeTerrainMeshService {
  public int ActivateCalls { get; private set; }

  public int DeactivateCalls { get; private set; }

  public void Activate() {
    ActivateCalls++;
  }

  public void Deactivate() {
    DeactivateCalls++;
  }
}
