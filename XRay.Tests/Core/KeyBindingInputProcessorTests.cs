using IgorZ.XRay.Core;
using Timberborn.InputSystem;

namespace XRay.Tests;

static class KeyBindingInputProcessorTests {
  public static void RegistersItself() {
    var input = new InputService();
    var (manager, _, _, _) = CreateManager();
    var processor = CreateProcessor(input, manager);

    processor.PostLoad();

    Assert.Same(processor, input.RegisteredProcessor);
  }

  public static void ActivatesOnHold() {
    var input = new InputService();
    var (manager, transparent, naturalResources, wireframe) = CreateManager();
    var processor = CreateProcessor(input, manager);

    input.HeldKeyId = KeyBindingInputProcessor.ShowModeBindingKey;
    processor.ProcessInput();
    processor.ProcessInput();

    Assert.True(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);

    input.HeldKeyId = null;
    processor.ProcessInput();

    Assert.False(manager.IsActive);
    Assert.Equal(1, transparent.DeactivateCalls);
    Assert.Equal(1, naturalResources.DeactivateCalls);
    Assert.Equal(1, wireframe.DeactivateCalls);
  }

  public static void IgnoresHoldWhenActive() {
    var input = new InputService();
    var (manager, transparent, naturalResources, wireframe) = CreateManager();
    var processor = CreateProcessor(input, manager);
    manager.SetActiveMode(true);

    input.HeldKeyId = KeyBindingInputProcessor.ShowModeBindingKey;
    processor.ProcessInput();
    input.HeldKeyId = null;
    processor.ProcessInput();

    Assert.True(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);
    Assert.Equal(0, transparent.DeactivateCalls);
    Assert.Equal(0, naturalResources.DeactivateCalls);
    Assert.Equal(0, wireframe.DeactivateCalls);
  }

  static KeyBindingInputProcessor CreateProcessor(InputService inputService, XRayModeManager manager) {
    return new KeyBindingInputProcessor(manager, inputService);
  }

  static (
      XRayModeManager Manager, TransparentTerrainMeshService Transparent,
      NaturalResourceVisibilityService NaturalResources, WireframeTerrainMeshService Wireframe) CreateManager() {
    var transparent = new TransparentTerrainMeshService();
    var naturalResources = new NaturalResourceVisibilityService();
    var wireframe = new WireframeTerrainMeshService();
    var manager = TestObjectFactory.Create<XRayModeManager>(
        ("_transparentTerrainMeshService", transparent),
        ("_naturalResourceVisibilityService", naturalResources),
        ("_wireframeTerrainMeshService", wireframe));
    return (manager, transparent, naturalResources, wireframe);
  }
}
