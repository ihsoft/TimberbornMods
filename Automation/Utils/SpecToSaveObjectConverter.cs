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

    /// <summary>String value.</summary>
    [Serialize(isOptional: true)]
    public string StrValue { get; init; }

    /// <summary>Integer value.</summary>
    [Serialize(isOptional: true)]
    public int? IntValue { get; init; }

    /// <summary>Float value.</summary>
    [Serialize(isOptional: true)]
    public int? FloatValue { get; init; }

    /// <summary>Boolean value.</summary>
    [Serialize(isOptional: true)]
    public bool? BoolValue { get; init; }
  }

  /// <summary>Converts the parameters to the object save.</summary>
  public static ObjectSave ParametersToSaveObject(IEnumerable<AutomationParameterSpec> parameters) {
    var dict = new Dictionary<string, object>();
    foreach (var parameter in parameters) {
      if (parameter.StrValue != null) {
        dict[parameter.Name] = parameter.StrValue;
      } else if (parameter.IntValue.HasValue) {
        dict[parameter.Name] = parameter.IntValue;
      } else if (parameter.FloatValue.HasValue) {
        dict[parameter.Name] = parameter.FloatValue;
      } else if (parameter.BoolValue.HasValue) {
        dict[parameter.Name] = parameter.BoolValue;
      } else {
        throw new InvalidDataException($"No value provided for the parameter: {parameter.Name}");
      }
    }
    return new ObjectSave(dict);
  }
}
