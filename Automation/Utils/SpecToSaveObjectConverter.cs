// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.BlueprintSystem;
using Timberborn.SerializationSystem;

namespace IgorZ.Automation.Utils;

/// <summary>Converts the spec parameters to the object values.</summary>
public static class SpecToSaveObjectConverter {

  /// <summary>Parameter definition for the spec.</summary>
  public record AutomationParameterSpec {
    /// <summary>Parameter name.</summary>
    [Serialize]
    public string Name { get; init; }

    /// <summary>Parameter value.</summary>
    /// <remarks>
    /// The string values must be quoted in single quotes. Valid values for the boolean values are: "true" or "false".
    /// In any register. Numbers must start from 0-9 symbol. The number is tried to be parsed as an integer first, then
    /// as a float.
    /// </remarks>
    [Serialize]
    public string Value { get; init; }
  }

  /// <summary>Converts the parameters to the object save.</summary>
  public static ObjectSave ParametersToSaveObject(IEnumerable<AutomationParameterSpec> parameters) {
    return new ObjectSave(
        parameters.ToDictionary(x => x.Name, x => ParseParameterAsObject(x.Name, x.Value)));
  }

  /// <summary>
  /// Parses the parameter value as an object. The returned object can be int, float, string, or boolean.
  /// </summary>
  /// <remarks>
  /// This method tries to guess the type from the syntax. The strings must be quoted with single quotes, for example,
  /// 'my string'. The boolean type must be a single word of "true" or "false" in any register. Anything that starts
  /// from 0-9 symbol is tried to be parsed as a number: first as an integer, then as a float. If the number must be a
  /// float, it has to have the "." decimal delimiter. For example, "1.0" is a float, but "1" is an integer.
  /// </remarks>
  public static object ParseParameterAsObject(string name, string value) {
    if (value.Length < 1) {
      throw new InvalidDataException($"Empty parameter values are not allowed: name={name}");
    }
    if (value.Length > 0 && value[0] == '\'') {
      if (value[^1] != '\'') {
        throw new InvalidDataException($"Unterminated string literal for parameter {name}: {value}");
      }
      return value[1..^1];
    }
    if (value[0] >= '0' && value[0] <= '9' || value[0] == '-') {
      if (int.TryParse(value, out var intValue)) {
        return intValue;
      }
      if (float.TryParse(value, out var floatValue)) {
        return floatValue;
      }
      throw new InvalidDataException($"Cannot parse parameter {name} as number: {value}");
    }
    if (value.ToLower() == "true") {
      return true;
    }
    if (value.ToLower() == "false") {
      return false;
    }
    throw new InvalidDataException($"Cannot parse parameter {name}: {value}");
  }
}
