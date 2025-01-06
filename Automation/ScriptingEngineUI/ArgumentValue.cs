// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentValue {
  public string StringValue;
  public int? NumberValue;

  public static implicit operator ArgumentValue(string value) => new() { StringValue = value };
  public static implicit operator ArgumentValue(int value) => new() { NumberValue = value };
  public static implicit operator string(ArgumentValue value) =>
      value.StringValue ?? value.NumberValue?.ToString() ?? "null";
}
