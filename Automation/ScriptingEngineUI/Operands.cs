// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

static class Operands {
  public enum OperandType {
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
  }

  //FIXME: translate
  public static string ToString(OperandType operandType) {
    return operandType switch {
      OperandType.Equal => "равно",
      OperandType.NotEqual => "не равно",
      OperandType.Greater => "больше",
      OperandType.GreaterOrEqual => "больше или равно",
      OperandType.Less => "меньше",
      OperandType.LessOrEqual => "меньше или равно",
      _ => throw new System.ArgumentOutOfRangeException(nameof(operandType), operandType, null)
    };
  }

  public static DropdownItem<OperandType> ToDropdownItem(OperandType operandType) {
    return new DropdownItem<OperandType> { Value = operandType, Text = ToString(operandType) };
  }

  public static DropdownItem<OperandType>[] ToDropdownItems(OperandType[] types) {
    return types.Select(ToDropdownItem).ToArray();
  }

  public static readonly OperandType[] All = [
      OperandType.Equal,
      OperandType.NotEqual,
      OperandType.Greater,
      OperandType.GreaterOrEqual,
      OperandType.Less,
      OperandType.LessOrEqual
  ];
}
