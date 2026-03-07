// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.TimberDev.UI;

namespace IgorZ.Automation.ScriptingEngineUI;

sealed record ArgumentDefinition {

  const string ConstantTypeStringLocKey = "IgorZ.Automation.Scripting.Editor.ConstantTypeString";
  const string ConstantTypeWholeNumberLocKey = "IgorZ.Automation.Scripting.Editor.ConstantTypeWholeNumber";
  const string ConstantTypeDecimalsLocKey = "IgorZ.Automation.Scripting.Editor.ConstantTypeDecimals";
  const string ConstantTypePercentLocKey = "IgorZ.Automation.Scripting.Editor.ConstantTypePercent";

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
      var locValue = valueDef.ValueType switch {
          ScriptValue.TypeEnum.String => uiFactory.T(ConstantTypeStringLocKey),
          ScriptValue.TypeEnum.Number => valueDef.DisplayNumericFormat switch {
              ValueDef.NumericFormatEnum.Integer => uiFactory.T(ConstantTypeWholeNumberLocKey),
              ValueDef.NumericFormatEnum.Float => uiFactory.T(ConstantTypeDecimalsLocKey),
              ValueDef.NumericFormatEnum.Percent => uiFactory.T(ConstantTypePercentLocKey),
              _ => throw new InvalidOperationException($"Unsupported numeric format: {valueDef.DisplayNumericFormat}"),
          },
          _ => throw new InvalidOperationException($"Unsupported value type: {valueDef.ValueType}"),
      };
      ValueOptions = [(ArgumentConstructor.InputTypeName, locValue)];
    }
  }
}
