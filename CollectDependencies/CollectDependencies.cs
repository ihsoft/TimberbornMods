// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CollectDependencies {

// ReSharper disable once ClassNeverInstantiated.Global
public class CollectDependencies {

  /// <summary>Assemblies we don't care about.</summary>
  /// <remarks>They will be somehow resolved inside Unity.</remarks>
  static readonly List<string> SkipPrefixes = new() {
      "System.Web",
      "System.Xml",
      "System.Data",
      "System.Design",
      "System.ServiceModel",
      "System.Runtime.Serialization",
      "System.EnterpriseServices",
  };

  static readonly List<string> SkipDllPrefixes = new() {
      "mscorlib.",
      "netstandard.",
      "System.",
      "UnityEngine.",
  };

  /// <summary>Captures all the DLLs from the game target folders.</summary>
  static readonly Dictionary<string, string> AllDependencies = new();

  static string _gameBasePath;

  static int Main(string[] args) {
    if (args.Length != 1 && args.Length != 2) {
      Console.WriteLine("To print all the dependencies:");
      Console.WriteLine("  CollectDependencies <targetDLL>");
      Console.WriteLine();
      Console.WriteLine("To copy all the dependency DLLs into the target folder:");
      Console.WriteLine("  CollectDependencies <targetDLL> <outputDir>");
      Console.WriteLine();
      Console.WriteLine(
          "The <targetDLL> path must be pointing to a DLL, that is located INSIDE the game's main folder.");
      return 1;
    }

    var pathToDll = Path.GetFullPath(args[0]);
    Console.WriteLine($"Getting dependencies for {pathToDll}...");
    if (!GuessGameFolderFromPath(pathToDll)) {
      Console.WriteLine("Cannot guess the game base directory!");
      return -1;
    }
    Console.WriteLine($"Game's base directory detected: {_gameBasePath}");

    if (args.Length == 2) {
      var output = Path.GetFullPath(args[1]);
      if (!Directory.Exists(output)) {
        Console.WriteLine($"The output directory doesn't exist {output}...");
        return -1;
      }
      Console.WriteLine($"Writing to {output}...");
    }

    LoadAssembliesFromFolder("Timberborn_Data/Managed");
    LoadAssembliesFromFolder("BepInEx/core");
    LoadAssembliesFromFolder("BepInEx/plugins");

    var dependencies = new Dictionary<string, string>();
    var unknown = new List<string>();
    GatherDependencies(pathToDll, ref dependencies, ref unknown);
    if (unknown.Count > 0) {
      Console.WriteLine("*** UNRESOLVED DEPENDENCIES");
      foreach (var name in unknown) {
        Console.WriteLine(name);
      }
      Console.WriteLine();
    }

    if (args.Length == 1) {
      Console.WriteLine($"\nThe DLLs that are required by {pathToDll}:");
      var sortedKeys = dependencies.Keys.ToList();
      sortedKeys.Sort();
      foreach (var assemblyName in sortedKeys) {
        Console.WriteLine(assemblyName);
      }
      return 0;
    }

    if (args.Length == 2) {
      Console.WriteLine($"Copy {dependencies.Count} depencdecies...");
      var output = Path.GetFullPath(args[1]);
      foreach (var location in dependencies.Values) {
        var dllName = Path.GetFileName(location);
        if (SkipDllPrefixes.Any(x => dllName.StartsWith(x))) {
          continue;
        }
        File.Copy(location, Path.Combine(output, Path.GetFileName(location)), true);
      }
    }

    return 0;
  }

  static bool GuessGameFolderFromPath(string path) {
    var testPath = path.ToLower();
    int pos = -1;

    pos = testPath.IndexOf("bepinex", StringComparison.Ordinal);
    if (pos != -1) {
      Console.WriteLine("Detected a game's plugin.");
      _gameBasePath = path.Substring(0, pos);
      return true;
    }
    pos = testPath.IndexOf("timberborn_data", StringComparison.Ordinal);
    if (pos != -1) {
      Console.WriteLine("Detected a game's managed DLL.");
      _gameBasePath = path.Substring(0, pos);
      return true;
    }
    return false;
  }

  static void GatherDependencies(string fullName, ref Dictionary<string, string> collectedNames, ref List<string> unknown) {
    var assembly = Assembly.LoadFile(fullName);
    if (SkipPrefixes.Any(x => assembly.GetName().Name.StartsWith(x))) {
      return;
    }
    collectedNames[assembly.GetName().Name] = fullName;
    var dependencies = assembly.GetReferencedAssemblies();
    var needToResolve = new HashSet<string>();
    foreach (var dependency in dependencies) {
      var assemblyName = dependency.Name;
      if (!AllDependencies.ContainsKey(assemblyName)) {
        unknown.Add(assemblyName);
        continue;
      }
      if (!collectedNames.ContainsKey(assemblyName)) {
        needToResolve.Add(assemblyName);
      }
    }
    foreach (var dependency in needToResolve) {
      if (collectedNames.ContainsKey(dependency)) {
        continue;
      }
      GatherDependencies(AllDependencies[dependency], ref collectedNames, ref unknown);
    }
  }

  static void LoadAssembliesFromFolder(string folder) {
    var fullPath = Path.Combine(_gameBasePath, folder);
    Console.WriteLine($"Reading all DLLs from: {fullPath}...");
    var dlls = Directory.GetFiles(fullPath, "*.dll", SearchOption.AllDirectories);
    var readFiles = 0;
    foreach (var dllPath in dlls) {
      try {
        var assembly = Assembly.LoadFile(dllPath);
        AllDependencies[assembly.GetName().Name] = dllPath;
        readFiles++;
      } catch (FileLoadException e) {
        Console.WriteLine($"*** Cannot load DLL: {dllPath}\nError: {e.Message}");
      }
    }
    Console.WriteLine($"...got {readFiles} DLLs");
  }
}

}
