using Timberborn.BlueprintSystem;

namespace Timberborn.KeyBindingSystem;

public class KeyBinding {
  public readonly KeyBindingDefinition _keyBindingDefinition;

  public KeyBinding(string id, bool isDown, KeyBindingDefinition keyBindingDefinition) {
    Id = id;
    IsDown = isDown;
    _keyBindingDefinition = keyBindingDefinition;
  }

  public string Id { get; }

  public bool IsDown { get; set; }
}

public class KeyBindingDefinition {
  public KeyBindingDefinition(KeyBindingSpec keyBindingSpec) {
    KeyBindingSpec = keyBindingSpec;
  }

  public KeyBindingSpec KeyBindingSpec { get; }
}

public record KeyBindingSpec : ComponentSpec {
  public string Id { get; init; }
}
