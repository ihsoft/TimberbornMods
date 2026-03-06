// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed record ArgumentDefinition {

  const string StringConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.StringConstantType";
  const string NumberConstantTypeLocKey = "IgorZ.Automation.Scripting.Editor.NumberConstantType";

  /// <inheritdoc cref="ScriptingEngine.ScriptableComponents.ValueDef.ValueType"/>
  public ScriptValue.TypeEnum ValueType => ValueDef.ValueType;

  /// <summary>If the set of the string values is limited, this is the set.</summary>
  /// <remarks>If not provided (null), then it is a free form value.</remarks>
  public DropdownItem<string>[] ValueOptions { get; }

  /// <summary>The value definition this argument is bound to.</summary>
  public ValueDef ValueDef { get; }

  public ArgumentDefinition(UiFactory uiFactory, ValueDef valueDef) {
    ValueDef = valueDef;
    if (valueDef.Options != null) {
      ValueOptions = valueDef.Options;
    } else {
      ValueOptions = valueDef.ValueType == ScriptValue.TypeEnum.Number
          ? [(ArgumentConstructor.InputTypeName, uiFactory.T(NumberConstantTypeLocKey))]
          : [(ArgumentConstructor.InputTypeName, uiFactory.T(StringConstantTypeLocKey))];
    }
  }
}

