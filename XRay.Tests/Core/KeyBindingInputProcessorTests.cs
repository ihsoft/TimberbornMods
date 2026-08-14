using IgorZ.XRay.Core;
using Timberborn.InputSystem;

namespace XRay.Tests;

static class KeyBindingInputProcessorTests {
  public static void RegistersItself() {
    var input = new InputService();
    var (manager, _, _, _, _) = CreateManager();
    var processor = CreateProcessor(input, manager);

    processor.PostLoad();

    Assert.Same(processor, input.RegisteredProcessor);
  }

  public static void ActivatesOnHold() {
    var input = new InputService();
    var (manager, transparent, _, _, wireframe) = CreateManager();
    var processor = CreateProcessor(input, manager);

    input.HeldKeyId = KeyBindingInputProcessor.ShowModeBindingKey;
    processor.ProcessInput();
    processor.ProcessInput();

    Assert.True(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);

    input.HeldKeyId = null;
    processor.ProcessInput();

    Assert.False(manager.IsActive);
    Assert.Equal(1, transparent.DeactivateCalls);
    Assert.Equal(1, wireframe.DeactivateCalls);
  }

  public static void IgnoresHoldWhenActive() {
    var input = new InputService();
    var (manager, transparent, _, _, wireframe) = CreateManager();
    var processor = CreateProcessor(input, manager);
    manager.SetActiveMode(true);

    input.HeldKeyId = KeyBindingInputProcessor.ShowModeBindingKey;
    processor.ProcessInput();
    input.HeldKeyId = null;
    processor.ProcessInput();

    Assert.True(manager.IsActive);
    Assert.Equal(1, transparent.ActivateCalls);
    Assert.Equal(1, wireframe.ActivateCalls);
    Assert.Equal(0, transparent.DeactivateCalls);
    Assert.Equal(0, wireframe.DeactivateCalls);
  }

  public static void ObjectTransparencyRequiresXRay() {
    var input = new InputService { DownKeyId = KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey };
    var (manager, _, buildings, naturalResources, _) = CreateManager();
    var processor = CreateProcessor(input, manager);

    processor.ProcessInput();

    Assert.Equal(0, buildings.ActivateCalls);
    Assert.Equal(0, naturalResources.ActivateCalls);

    input.DownKeyId = null;
    manager.SetActiveMode(true);
    processor.ProcessInput();
    input.DownKeyId = KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey;
    processor.ProcessInput();

    Assert.Equal(1, buildings.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);

    input.DownKeyId = null;
    manager.SetActiveMode(false);
    processor.ProcessInput();
    processor.ProcessInput();

    Assert.Equal(1, buildings.DeactivateCalls);
    Assert.Equal(1, naturalResources.DeactivateCalls);
  }

  public static void ShortPressTogglesObjectTransparency() {
    var input = new InputService();
    var (manager, _, buildings, naturalResources, _) = CreateManager();
    var processor = CreateProcessor(input, manager);
    manager.SetActiveMode(true);
    processor.ProcessInput();

    PressObjectTransparency(input, processor, shortPress: true);

    Assert.True(buildings.IsActive);
    Assert.True(naturalResources.IsActive);

    PressObjectTransparency(input, processor, shortPress: true);

    Assert.False(buildings.IsActive);
    Assert.False(naturalResources.IsActive);
  }

  public static void HeldObjectTransparencyIsTemporary() {
    var input = new InputService();
    var (manager, _, buildings, naturalResources, _) = CreateManager();
    var processor = CreateProcessor(input, manager);
    manager.SetActiveMode(true);
    processor.ProcessInput();

    PressObjectTransparency(input, processor, shortPress: false);

    Assert.Equal(1, buildings.ActivateCalls);
    Assert.Equal(1, naturalResources.ActivateCalls);
    Assert.Equal(1, buildings.DeactivateCalls);
    Assert.Equal(1, naturalResources.DeactivateCalls);
  }

  static void PressObjectTransparency(InputService input, KeyBindingInputProcessor processor, bool shortPress) {
    input.DownKeyId = KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey;
    processor.ProcessInput();
    input.DownKeyId = null;
    input.UpKeyId = KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey;
    input.ShortHeldKeyId = shortPress ? KeyBindingInputProcessor.ToggleObjectTransparencyBindingKey : null;
    processor.ProcessInput();
    input.UpKeyId = null;
    input.ShortHeldKeyId = null;
  }

  static KeyBindingInputProcessor CreateProcessor(InputService inputService, XRayModeManager manager) {
    return new KeyBindingInputProcessor(manager, inputService);
  }

  static (
      XRayModeManager Manager, TransparentTerrainMeshService Transparent,
      TransparentBuildingModelService Buildings,
      TransparentNaturalResourceModelService NaturalResources,
      WireframeTerrainMeshService Wireframe) CreateManager() {
    var transparent = new TransparentTerrainMeshService();
    var transparentBuildings = new TransparentBuildingModelService();
    var transparentNaturalResources = new TransparentNaturalResourceModelService();
    var wireframe = new WireframeTerrainMeshService();
    var manager = TestObjectFactory.Create<XRayModeManager>(
        ("_transparentTerrainMeshService", transparent),
        ("_transparentBuildingModelService", transparentBuildings),
        ("_transparentNaturalResourceModelService", transparentNaturalResources),
        ("_wireframeTerrainMeshService", wireframe));
    return (manager, transparent, transparentBuildings, transparentNaturalResources, wireframe);
  }
}
