// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

public static class Operands {
  public enum Type {
    Equal,
    NotEqual,
    Greater,
    GreaterOrEqual,
    Less,
    LessOrEqual,
  }

  //FIXME: translate
  public static string ToString(Type type) {
    return type switch {
      Type.Equal => "равно",
      Type.NotEqual => "не равно",
      Type.Greater => "больше",
      Type.GreaterOrEqual => "больше или равно",
      Type.Less => "меньше",
      Type.LessOrEqual => "меньше или равно",
      _ => throw new System.ArgumentOutOfRangeException(nameof(type), type, null)
    };
  }

  public static DropdownItem<Type> ToDropdownItem(Type type) {
    return new DropdownItem<Type> { Value = type, Text = ToString(type) };
  }

  public static DropdownItem<Type>[] ToDropdownItems(Type[] types) {
    return types.Select(ToDropdownItem).ToArray();
  }

  public static readonly Type[] All = [
      Type.Equal,
      Type.NotEqual,
      Type.Greater,
      Type.GreaterOrEqual,
      Type.Less,
      Type.LessOrEqual
  ];
}
