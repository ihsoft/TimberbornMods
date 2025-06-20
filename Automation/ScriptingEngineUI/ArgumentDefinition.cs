// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed class ArgumentDefinition {

  const string StringConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.StringConstantType";
  const string NumberConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.NumberConstantType";

  /// <summary>The type of this value.</summary>
  public ScriptValue.TypeEnum ValueType { get; }

  /// <summary>Function to validate the value of this argument.</summary>
  /// <exception cref="ScriptError.BadValue">if the value is invalid.</exception>
  public Action<ScriptValue> ValueValidator { get; }

  /// <summary>If the set of the string values is limited, this is the set.</summary>
  /// <remarks>If not provided (null), then it is a free form value.</remarks>
  public DropdownItem<string>[] ValueOptions { get; }

  /// <summary>Optional hint text to show in UI for the argument.</summary>
  /// <remarks>Set to "null" if no hint is needed.</remarks>
  public string ValueUiHint { get; }

  public ArgumentDefinition(UiFactory uiFactory, ValueDef valueDef) {
    ValueType = valueDef.ValueType;
    ValueValidator = valueDef.ValueValidator;
    ValueUiHint = valueDef.ValueUiHint;
    if (valueDef.Options != null) {
      ValueOptions = valueDef.Options;
    } else {
      ValueOptions = valueDef.ValueType == ScriptValue.TypeEnum.Number
          ? [(ArgumentConstructor.InputTypeName, uiFactory.T(NumberConstantTypeLocKey))]
          : [(ArgumentConstructor.InputTypeName, uiFactory.T(StringConstantTypeLocKey))];
    }
  }
}

