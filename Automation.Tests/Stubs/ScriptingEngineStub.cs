namespace IgorZ.Automation.Settings {
  static class ScriptEngineSettings {
    public static bool CheckArgumentValues => true;
    public static int SignalExecutionStackSize => 10;
  }

  static class AutomationDebugSettings {
    public static bool LogSignalsPropagating => false;
    public static bool LogSignalsSetting => false;
    public static bool ReevaluateRulesOnLoad => true;
    public static bool ResetSignalsOnLoad => false;
  }

  static class ScriptEditorSettings {
    public enum ScriptSyntax {
      Lisp,
      Python,
    }

    public static ScriptSyntax DefaultScriptSyntax { get; set; } = ScriptSyntax.Python;
  }

  static class EntityPanelSettings {
    public static bool EvalValuesInConditions => false;
    public static bool EvalValuesInActionArguments => false;
    public static bool ShowOptionalLogicalParentheses { get; set; } = true;
  }
}

namespace IgorZ.TimberDev.UI {
  static class CommonFormats {
    public static string HighlightRed(string value) => value;
  }
}

namespace IgorZ.TimberDev.UI {
  using UnityEngine;

  public sealed class StatusSpriteLoader {
    public Sprite LoadSprite(string spriteName) {
      return new Sprite();
    }
  }

  public record struct DropdownItem {
    public string Value { get; init; }
    public string Text { get; init; }
    public Sprite Icon { get; init; }

    public static implicit operator DropdownItem((string value, string text) tuple) {
      return new DropdownItem { Value = tuple.value, Text = tuple.text };
    }

    public static implicit operator DropdownItem((string value, Sprite icon, string text) tuple) {
      return new DropdownItem { Value = tuple.value, Icon = tuple.icon, Text = tuple.text };
    }
  }
}

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components {
  static class DictionaryExtensions {
    public static TValue GetOrAdd<TKey, TValue>(this System.Collections.Generic.Dictionary<TKey, TValue> dictionary,
                                                TKey key)
        where TValue : new() {
      if (!dictionary.TryGetValue(key, out var value)) {
        value = new TValue();
        dictionary.Add(key, value);
      }
      return value;
    }
  }
}

namespace IgorZ.TimberDev.Utils {
  static class StringProtoSerializer {
    static readonly System.Collections.Generic.Dictionary<string, object> SerializedObjects = new();

    public static string Serialize<T>(T obj) {
      var key = System.Guid.NewGuid().ToString();
      SerializedObjects.Add(key, obj);
      return key;
    }

    public static T Deserialize<T>(string text) {
      return (T)SerializedObjects[text];
    }
  }
}
