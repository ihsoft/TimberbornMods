// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Versioned declarations read from a mod's AutomationHooks*.json files; contains no runtime state.</summary>
sealed class HookConfiguration {
  /// <summary>Required format version; unknown versions reject the file.</summary>
  public int Version { get; set; }
  /// <summary>Provider declarations in registration order.</summary>
  public Provider[] Providers { get; set; } = [];

  /// <summary>Stable script namespace and editor origin for a group of capabilities.</summary>
  public sealed class Provider {
    /// <summary>Script-safe namespace segment, preserved in saved rules and shown as the editor origin.</summary>
    public string Id { get; set; }
    /// <summary>Classes whose members supply this provider's capabilities.</summary>
    public Target[] Targets { get; set; } = [];
  }

  /// <summary>Runtime type identity and ownership; no assembly-qualified strings or DLL paths are used.</summary>
  public class Source {
    /// <summary>Loaded assembly's simple name without the DLL suffix.</summary>
    public string Assembly { get; set; }
    /// <summary>Full CLR type name.</summary>
    public string Type { get; set; }
    /// <summary>Singleton (default), Building or Static.</summary>
    public string Scope { get; set; } = "Singleton";
  }

  /// <summary>Capabilities sharing one source type.</summary>
  public sealed class Target : Source {
    /// <summary>Values and their setter observations.</summary>
    public Signal[] Signals { get; set; } = [];
    /// <summary>Invocable methods and their ordered argument metadata.</summary>
    public Action[] Actions { get; set; } = [];
  }

  /// <summary>Pure value projection and the setters which can change it.</summary>
  public sealed class Signal {
    /// <summary>Stable capability ID within the provider.</summary>
    public string Id { get; set; }
    /// <summary>Localized signal label.</summary>
    public string NameLocKey { get; set; }
    /// <summary>Dot path of properties or parameterless methods spelled Name().</summary>
    public string Read { get; set; }
    /// <summary>Setter property names; null uses Read's first property, empty means no updates.</summary>
    public string[] Watch { get; set; }
    /// <summary>Editor metadata; the actual CLR result type is inferred from Read.</summary>
    public Value Value { get; set; } = new();
  }

  /// <summary>A void method with any fixed number of supported parameters.</summary>
  public sealed class Action {
    /// <summary>Stable capability ID within the provider.</summary>
    public string Id { get; set; }
    /// <summary>Localized action label, optionally containing numbered argument placeholders.</summary>
    public string NameLocKey { get; set; }
    /// <summary>CLR method name; overload selection uses Arguments' exact types.</summary>
    public string Method { get; set; }
    /// <summary>Ordered parameters; an empty array declares a parameterless action.</summary>
    public Value[] Arguments { get; set; } = [];
  }

  /// <summary>Metadata shared by signal results and action arguments.</summary>
  public sealed class Value {
    /// <summary>bool, int, float, double or string; optional only for signal results.</summary>
    public string Type { get; set; }
    /// <summary>Optional argument label in a multi-argument editor.</summary>
    public string NameLocKey { get; set; }
    /// <summary>Display a float/double fraction as whole percentages.</summary>
    public bool Percent { get; set; }
    /// <summary>Optional inclusive lower editor bound, in provider units even for percentages.</summary>
    public double? Min { get; set; }
    /// <summary>Optional inclusive upper editor bound; neither bound constrains runtime values.</summary>
    public double? Max { get; set; }
    /// <summary>Fixed string choices, mutually exclusive with OptionsSource.</summary>
    public Option[] Options { get; set; }
    /// <summary>Registry-backed string choices, captured once during PostLoad.</summary>
    public OptionSource OptionsSource { get; set; }
  }

  /// <summary>Stable string value and localized dropdown label.</summary>
  public sealed class Option {
    /// <summary>Exact stored string; must be unique within a dropdown.</summary>
    public string Value { get; set; }
    /// <summary>Non-empty localization key for the dropdown item.</summary>
    public string NameLocKey { get; set; }
  }

  /// <summary>Shared registry projection for a string dropdown.</summary>
  public sealed class OptionSource : Source {
    /// <summary>Path returning IEnumerable&lt;T&gt; from a Singleton or Static source.</summary>
    public string Read { get; set; }
    /// <summary>String-valued path on each registry item.</summary>
    public string ValuePath { get; set; }
    /// <summary>Localization-key path on each item; empty keys omit hidden registry entries.</summary>
    public string NameLocKeyPath { get; set; }
  }
}
