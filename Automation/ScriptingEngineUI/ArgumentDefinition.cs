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

  public ScriptValue.TypeEnum ValueType { get; }
  public Action<ScriptValue> ValueValidator { get; }
  public DropdownItem<string>[] ValueOptions { get; }

  public ArgumentDefinition(UiFactory uiFactory, ValueDef valueDef) {
    ValueType = valueDef.ValueType;
    ValueValidator = valueDef.ValueValidator;
    if (valueDef.Options != null) {
      ValueOptions = valueDef.Options;
    } else {
      ValueOptions = valueDef.ValueType == ScriptValue.TypeEnum.Number
          ? [(ArgumentConstructor.InputTypeName, uiFactory.T(NumberConstantTypeLocKey))]
          : [(ArgumentConstructor.InputTypeName, uiFactory.T(StringConstantTypeLocKey))];
    }
  }
}

