// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.ScriptingEngine.Expressions;
using ProtoBuf;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>Shared signal save format. Field numbers preserve existing building saves.</summary>
[ProtoContract]
sealed record SavedSignalValues {
  /// <summary>A signal name and its string or fixed-precision numeric value.</summary>
  [ProtoContract]
  public sealed record SignalValue {
    /// <summary>Full persistent script identity; never a localized display label.</summary>
    [ProtoMember(1)]
    public string SignalName { get; init; }
    /// <summary>String payload; null selects the numeric payload. Empty strings remain strings.</summary>
    [ProtoMember(2)]
    public string StrValue { get; init; }
    /// <summary>Raw scaled integer, used only when the string payload is null.</summary>
    [ProtoMember(3)]
    public int NumValue { get; init; }

    /// <summary>Preserves a valid value without floating-point conversion; callers omit unset values.</summary>
    public static SignalValue FromScriptValue(string signalName, ScriptValue value) {
      return new SignalValue {
          SignalName = signalName,
          StrValue = value.ValueType == ScriptValue.TypeEnum.String ? value.AsString : null,
          NumValue = value.ValueType == ScriptValue.TypeEnum.Number ? value.AsRawNumber : int.MinValue,
      };
    }

    /// <summary>Restores the original string or exact raw numeric representation.</summary>
    public ScriptValue ToScriptValue() {
      return StrValue != null ? ScriptValue.FromString(StrValue) : ScriptValue.Of(NumValue);
    }
  }

  /// <summary>Cached values shared by the building and global tracker save contracts.</summary>
  [ProtoMember(1)]
  public SignalValue[] SignalValues { get; init; }
}
