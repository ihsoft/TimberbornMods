using System;
using IgorZ.CustomTools.Core;
using IgorZ.CustomTools.KeyBindings;
using IgorZ.CustomTools.Tools;
using Timberborn.InputSystem;
using Timberborn.KeyBindingSystem;
using Timberborn.SingletonSystem;

namespace CustomTools.Tests;

static class KeyBindingInputProcessorTests {
  public static void RegistersItselfAndClearsStateOnPostLoad() {
    var inputService = new InputService();
    var processor = CreateProcessor(inputService: inputService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding("stale", new CustomToolBindingSpec()));
    processor.ConsumeKeyBinding("stale");

    processor.PostLoad();

    Assert.Same(processor, inputService.RegisteredProcessor);
    Assert.Equal(0, KeyBindingInputProcessor.PressedKeyBindings.Count);
    Assert.False(processor.ProcessInput());
  }

  public static void SelectsBlockObjectToolAndBlocksConsumedKey() {
    var tool = new TestTool();
    var customToolsService = new CustomToolsService();
    customToolsService.BlockObjectTools["Levee"] = tool;
    var inputService = new InputService {
        DownKeyId = "levee-key",
    };
    var processor = CreateProcessor(customToolsService, inputService: inputService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "levee-key",
        new CustomToolBindingSpec { BlockObjectBlueprint = "Levee" }));

    Assert.True(processor.ProcessInput());

    Assert.Same(tool, customToolsService.SelectedTool);
    Assert.Equal(0, KeyBindingInputProcessor.PressedKeyBindings.Count);

    inputService.DownKeyId = null;
    inputService.HeldKeyId = "levee-key";
    Assert.True(processor.ProcessInput());

    inputService.HeldKeyId = null;
    inputService.LongHeldKeyId = "levee-key";
    Assert.True(processor.ProcessInput());

    inputService.LongHeldKeyId = null;
    Assert.False(processor.ProcessInput());
  }

  public static void SelectsToolByType() {
    var customToolsService = new CustomToolsService();
    var processor = CreateProcessor(customToolsService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "tool-type-key",
        new CustomToolBindingSpec { Type = "Namespace.ToolType" }));

    processor.ProcessInput();

    Assert.Equal("Namespace.ToolType", customToolsService.SelectedToolType);
  }

  public static void SelectsToolByCustomToolId() {
    var customToolsService = new CustomToolsService();
    var processor = CreateProcessor(customToolsService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "tool-id-key",
        new CustomToolBindingSpec { CustomToolId = "PathPlusTool" }));

    processor.ProcessInput();

    Assert.Equal("PathPlusTool", customToolsService.SelectedToolId);
  }

  public static void ProcessesNewestPressedBindingFirst() {
    var customToolsService = new CustomToolsService();
    var processor = CreateProcessor(customToolsService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "first-key",
        new CustomToolBindingSpec { Type = "FirstTool" }));
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "second-key",
        new CustomToolBindingSpec { CustomToolId = "SecondTool" }));

    processor.ProcessInput();

    Assert.Equal(null, customToolsService.SelectedToolType);
    Assert.Equal("SecondTool", customToolsService.SelectedToolId);
  }

  public static void PostsEventForUnroutedBinding() {
    var eventBus = new EventBus();
    var processor = CreateProcessor(eventBus: eventBus);
    var binding = CreateBinding("event-key", new CustomToolBindingSpec());
    KeyBindingInputProcessor.PressedKeyBindings.Add(binding);

    Assert.False(processor.ProcessInput());

    var postedEvent = (CustomToolKeyBindingEvent)eventBus.LastEvent;
    Assert.Same(binding, postedEvent.KeyBinding);
    Assert.Same(binding._keyBindingDefinition.KeyBindingSpec.GetSpec<CustomToolBindingSpec>(),
        postedEvent.CustomToolBindingSpec);
  }

  public static void IgnoresBindingsWithoutCustomToolSpec() {
    var customToolsService = new CustomToolsService();
    var eventBus = new EventBus();
    var processor = CreateProcessor(customToolsService, eventBus);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding("plain-key", null));

    Assert.False(processor.ProcessInput());

    Assert.Equal(null, customToolsService.SelectedTool);
    Assert.Equal(null, eventBus.LastEvent);
  }

  public static void ThrowsWhenBlockObjectBlueprintIsUnknown() {
    var customToolsService = new CustomToolsService();
    var processor = CreateProcessor(customToolsService);
    KeyBindingInputProcessor.PressedKeyBindings.Add(CreateBinding(
        "unknown-blueprint-key",
        new CustomToolBindingSpec { BlockObjectBlueprint = "UnknownBlueprint" }));

    var exception = Assert.Throws<InvalidOperationException>(() => processor.ProcessInput());

    Assert.Equal("Cannot find tool for blueprint: UnknownBlueprint", exception.Message);
  }

  static KeyBindingInputProcessor CreateProcessor(
      CustomToolsService customToolsService = null, EventBus eventBus = null, InputService inputService = null) {
    KeyBindingInputProcessor.PressedKeyBindings.Clear();
    return new KeyBindingInputProcessor(
        customToolsService ?? new CustomToolsService(),
        eventBus ?? new EventBus(),
        inputService ?? new InputService());
  }

  static KeyBinding CreateBinding(string id, CustomToolBindingSpec customToolBindingSpec, bool isDown = true) {
    var keyBindingSpec = new KeyBindingSpec { Id = id };
    if (customToolBindingSpec != null) {
      keyBindingSpec.Blueprint.AddSpec(customToolBindingSpec);
    }
    return new KeyBinding(id, isDown, new KeyBindingDefinition(keyBindingSpec));
  }

  sealed class TestTool : AbstractCustomTool {
  }
}
