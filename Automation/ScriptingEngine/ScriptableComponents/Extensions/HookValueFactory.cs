// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.Localization;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Validates configuration metadata and creates the editor's native value definitions.</summary>
static class HookValueFactory {
  const string StateOnLocKey = "Automation.State.On";
  const string StateOffLocKey = "Automation.State.Off";

  /// <summary>Checks metadata before installing patches, without requiring localization or provider state.</summary>
  public static void Validate(Type type, HookConfiguration.Value definition, HookConfiguration.Option[] options) {
    if (type != typeof(string) && type != typeof(bool) && type != typeof(int)
        && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Unsupported configured value type: " + type);
    }
    if (definition.Percent && type != typeof(float) && type != typeof(double)) {
      throw new ArgumentException("Percentages require a float or double value.");
    }
    if (options != null && (type != typeof(string) || options.Length == 0
        || options.Any(x => x?.Value == null || string.IsNullOrWhiteSpace(x.NameLocKey))
        || options.Select(x => x.Value).Distinct().Count() != options.Length)) {
      throw new ArgumentException("Invalid configured value options.");
    }
    var min = definition.Min;
    var max = definition.Max;
    if ((min.HasValue || max.HasValue) && (type == typeof(string) || type == typeof(bool))) {
      throw new ArgumentException("Editor bounds require int, float or double.");
    }
    if (min.HasValue && (double.IsNaN(min.Value) || double.IsInfinity(min.Value))
        || max.HasValue && (double.IsNaN(max.Value) || double.IsInfinity(max.Value)) || min > max) {
      throw new ArgumentException("Editor bounds must be finite and Min must not exceed Max.");
    }
    if (definition.NameLocKey != null && string.IsNullOrWhiteSpace(definition.NameLocKey)) {
      throw new ArgumentException("Argument NameLocKey must not be blank.");
    }
  }

  /// <summary>Creates a native definition; bounds constrain editor input, never runtime values.</summary>
  public static ValueDef Create(Type type, HookConfiguration.Value definition,
      HookConfiguration.Option[] options, ILoc loc) {
    Validate(type, definition, options);
    var choices = options?.Select(option => new DropdownItem {
        Value = option.Value, Text = loc.T(option.NameLocKey),
    }).ToArray();
    if (type == typeof(bool)) {
      choices = [
          new DropdownItem { Value = "100", Text = loc.T(StateOnLocKey) },
          new DropdownItem { Value = "0", Text = loc.T(StateOffLocKey) },
      ];
    }
    var displayScale = definition.Percent ? 100 : 1;
    return new ValueDef {
        DisplayName = definition.NameLocKey == null ? null : loc.T(definition.NameLocKey),
        ValueType = type == typeof(string) ? ScriptValue.TypeEnum.String : ScriptValue.TypeEnum.Number,
        DisplayNumericFormat = type == typeof(string) ? ValueDef.NumericFormatEnum.Unspecified
            : type == typeof(bool) ? ValueDef.NumericFormatEnum.Boolean
            : definition.Percent ? ValueDef.NumericFormatEnum.Percent
            : type == typeof(int) ? ValueDef.NumericFormatEnum.Integer : ValueDef.NumericFormatEnum.Float,
        DisplayNumericFormatRange = definition.Min.HasValue || definition.Max.HasValue
            ? ((float)(definition.Min * displayScale ?? double.NaN),
                (float)(definition.Max * displayScale ?? double.NaN)) : null,
        Options = choices,
        RuntimeValueValidator = value => {
          HookValueConverter.FromScript(type, value);
          if (options != null && !options.Any(x => x.Value == value.AsString)) {
            throw new ScriptError.BadValue("Unknown configured option: " + value.AsString);
          }
        },
    };
  }
}
