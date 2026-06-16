namespace IgorZ.Automation.Settings {
  static class ScriptEngineSettings {
    public static bool CheckArgumentValues => true;
    public static int SignalExecutionStackSize => 10;
  }

  static class AutomationDebugSettings {
    public static bool LogSignalsPropagating => false;
  }

  static class ScriptEditorSettings {
    public enum ScriptSyntax {
      Lisp,
      Python,
    }

    public static ScriptSyntax DefaultScriptSyntax { get; set; } = ScriptSyntax.Python;
  }
}

namespace IgorZ.TimberDev.UI {
  public sealed record DropdownItem(string Value, string Text);
}

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components {
  static class InventoryScriptableComponent {
    public static Timberborn.BaseComponentSystem.BaseComponent GetInventory(
        Timberborn.BaseComponentSystem.BaseComponent building, bool throwIfNotFound = true) {
      return null;
    }
  }
}
