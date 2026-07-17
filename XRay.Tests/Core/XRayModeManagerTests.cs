using IgorZ.XRay.Core;

namespace XRay.Tests;

static class XRayModeManagerTests {
  public static void TogglesServices() {
    var transparent = new TransparentTerrainMeshService();
    var naturalResources = new NaturalResourceVisibilityService();
    var wireframe = new WireframeTerrainMeshService();
    var manager = CreateManager(transparent, naturalResources, wireframe);

    manager.SetActiveMode(true);

    Assert.True(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);
    Assert.Equal(0, transparent.DeactivateCalls);
    Assert.Equal(0, naturalResources.DeactivateCalls);
    Assert.Equal(0, wireframe.DeactivateCalls);

    manager.SetActiveMode(false);

    Assert.False(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);
    Assert.Equal(1, transparent.DeactivateCalls);
    Assert.Equal(1, naturalResources.DeactivateCalls);
    Assert.Equal(1, wireframe.DeactivateCalls);
  }

  public static void SkipsDuplicateStateChanges() {
    var transparent = new TransparentTerrainMeshService();
    var naturalResources = new NaturalResourceVisibilityService();
    var wireframe = new WireframeTerrainMeshService();
    var manager = CreateManager(transparent, naturalResources, wireframe);

    manager.SetActiveMode(true);
    manager.SetActiveMode(true);
    manager.SetActiveMode(false);
    manager.SetActiveMode(false);

    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);
    Assert.Equal(1, transparent.DeactivateCalls);
    Assert.Equal(1, naturalResources.DeactivateCalls);
    Assert.Equal(1, wireframe.DeactivateCalls);
  }

  static XRayModeManager CreateManager(
      TransparentTerrainMeshService transparent, NaturalResourceVisibilityService naturalResources,
      WireframeTerrainMeshService wireframe) {
    return TestObjectFactory.Create<XRayModeManager>(
        ("_transparentTerrainMeshService", transparent),
        ("_naturalResourceVisibilityService", naturalResources),
        ("_wireframeTerrainMeshService", wireframe));
  }
}
