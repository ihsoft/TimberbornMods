// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Definition of a signal that can be used in the scripting engine.</summary>
sealed record SignalDef {
  public enum ScopeEnum {
    Building,
    Global,
  }

  /// <summary>Unique name of the signal as it appears in the scripts.</summary>
  public string ScriptName { get; init; }

  /// <summary>Human-readable and localized name of the signal.</summary>
  public string DisplayName { get; init; }

  /// <summary>Localization key used to make a parameterized <see cref="DisplayName"/>.</summary>
  /// <remarks>Set this together with <see cref="DisplayNameArgument"/> only for parameterized signal names.</remarks>
  public string DisplayNameLocKey { get; init; }

  /// <summary>Value substituted into <see cref="DisplayNameLocKey"/> to make <see cref="DisplayName"/>.</summary>
  public string DisplayNameArgument { get; init; }

  /// <summary>Scope that owns the signal value.</summary>
  public ScopeEnum Scope { get; init; } = ScopeEnum.Building;

  /// <summary>Definition of the result value.</summary>
  public ValueDef Result { get; init; }
}
