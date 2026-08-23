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

class TransparentBuildingModelService {
  public int ActivateCalls { get; private set; }

  public int DeactivateCalls { get; private set; }

  public bool IsActive { get; private set; }

  public bool PassThroughSurfaceObjects => IsActive;

  public void SetActive(bool active) {
    if (active == IsActive) {
      return;
    }
    IsActive = active;
    if (active) {
      ActivateCalls++;
    } else {
      DeactivateCalls++;
    }
  }
}

class TransparentNaturalResourceModelService {
  public int ActivateCalls { get; private set; }

  public int DeactivateCalls { get; private set; }

  public bool IsActive { get; private set; }

  public void SetActive(bool active) {
    if (active == IsActive) {
      return;
    }
    IsActive = active;
    if (active) {
      ActivateCalls++;
    } else {
      DeactivateCalls++;
    }
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
