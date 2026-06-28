using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.ModdingTools.Common;
using UnityEditor;
using UnityEngine;

namespace Timberborn.ModdingTools.ModBuilding {
  public static class ModBuilderBatch {

    public static void Build() {
      try {
        var arguments = ParseArguments(Environment.GetCommandLineArgs());
        var modName = GetRequired(arguments, "mod");
        var modDefinition = new ModFinder().GetAllMods()
            .SingleOrDefault(mod => string.Equals(mod.Name, modName, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(modDefinition.Name)) {
          throw new InvalidOperationException($"Mod not found in Unity project: {modName}");
        }

        var settings = new ModBuilderSettings(
            GetBool(arguments, "buildCode", false),
            GetBool(arguments, "buildWindowsAssetBundle", true),
            GetBool(arguments, "buildMacAssetBundle", true),
            GetBool(arguments, "deleteFiles", true),
            GetBool(arguments, "buildZipArchive", false),
            GetValue(arguments, "compatibilityVersion", string.Empty));
        Debug.Log($"Building mod from command line: {modDefinition.Name}");
        if (!new ModBuilder(new[] { modDefinition }, settings).Build()) {
          throw new InvalidOperationException($"Mod build failed: {modDefinition.Name}");
        }
        Debug.Log($"Mod build completed: {modDefinition.Name}");
        EditorApplication.Exit(0);
      }
      catch (Exception e) {
        Debug.LogException(e);
        EditorApplication.Exit(1);
      }
    }

    static Dictionary<string, string> ParseArguments(string[] arguments) {
      var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      for (var i = 0; i < arguments.Length; i++) {
        var argument = arguments[i];
        if (!argument.StartsWith("-", StringComparison.Ordinal)) {
          continue;
        }

        var key = argument.TrimStart('-');
        if (i + 1 >= arguments.Length || arguments[i + 1].StartsWith("-", StringComparison.Ordinal)) {
          result[key] = "true";
          continue;
        }
        result[key] = arguments[++i];
      }
      return result;
    }

    static string GetRequired(Dictionary<string, string> arguments, string key) {
      var value = GetValue(arguments, key, string.Empty);
      if (string.IsNullOrWhiteSpace(value)) {
        throw new ArgumentException($"Missing required Unity batch argument: -{key}");
      }
      return value;
    }

    static string GetValue(Dictionary<string, string> arguments, string key, string defaultValue) {
      return arguments.TryGetValue(key, out var value) ? value : defaultValue;
    }

    static bool GetBool(Dictionary<string, string> arguments, string key, bool defaultValue) {
      if (!arguments.TryGetValue(key, out var value)) {
        return defaultValue;
      }
      return bool.TryParse(value, out var result)
          ? result
          : throw new ArgumentException($"Invalid boolean value for -{key}: {value}");
    }

  }
}
