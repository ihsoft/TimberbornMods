// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils {

/// <summary>Helper class to control enabling/disabling of specific features in the plugin.</summary>
/// <remarks>
/// <p>
/// If mod affects some global behavior that may not be desired by all the players, it makes sense to give an option to
/// disable it. Protect such functionality with a feature flag check and let players to decide.
/// </p>
/// <p>
/// The features list is stored in the file that should be stored in the same folder as the plugin DLL
/// (<see cref="FeaturesFilename"/>). The file should contain a set of lines: one line per feature. The name of the
/// feature must be a single world, consisting of [.a-zA-Z0-9] symbols. A prefix symbol "!" can be added to the feature
/// name to indicate that it must be disabled on load. Symbol '<c>#</c>' at the line start is treated as a comment and
/// the line is ignored.  
/// </p>
/// <p>
/// The plugin should call <see cref="ReadFeatures"/> from some common place to initialize it's settings. E.g. it can be
/// done from the configurator. 
/// </p>
/// <p>
/// One way of simplifying teh usage is declaring a static class <c>Features</c> that would read the definition on the
/// first use:
/// </p>
/// <example><code>
/// static class Features {
///   public bool MyFeatureFlag;
/// 
///   static Features() {
///     FeatureController.ReadFeatures(Consume);
///   }
///   static bool Consume(string name, bool enabled) {
///     if (name == "MyFeature") {
///       MyFeatureFlag = enabled;
///       return true;
///     }
///     return false;
///   }
/// }
/// </code></example>
/// </remarks>
public static class FeatureController {

  static readonly Regex FeatureNameCheck = new(@"^[.a-zA-Z0-9]+$");

  /// <summary>
  /// The name of file that holds the enabled features. It must be stored in the same folder as the plugin's DLL.
  /// </summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string FeaturesFilename = "TimberDev_Features.txt";

  /// <summary>Reads feature config file located at <see cref="FeaturesFilename"/>.</summary>
  /// <param name="consumeFn">
  /// The function that takes the parsed feature name and it's state. The function must return <c>true</c> if it
  /// recognized the feature name and accepted it.
  /// </param>
  /// <returns><c>false</c> if no file were found.</returns>
  public static bool ReadFeatures(Func<string, bool, bool> consumeFn) {
    var assembly = typeof(FeatureController).Assembly;
    var featuresFile = Path.Combine(Path.GetDirectoryName(assembly.Location)!, FeaturesFilename);
    if (!File.Exists(featuresFile)) {
      return false;
    }
    var features = File.ReadAllLines(featuresFile)
        .Select(x => x.Trim())
        .Where(x => x.Length > 0 && x[0] != '#')
        .ToList();
    DebugEx.Info("Loaded {0} feature definitions for {1}", features.Count, assembly.FullName);
    for (var i = 0; i < features.Count; i++) {
      var feature = features[i];
      var isEnabled = true;
      if (feature[0] == '!') {
        isEnabled = false;
        feature = feature.Substring(1);
      }
      if (!FeatureNameCheck.IsMatch(feature)) {
        DebugEx.Error("Bad feature name ignored: {0}", feature);
        continue;
      }
      if (consumeFn(feature, isEnabled)) {
        DebugEx.Info(isEnabled ? "Enable feature: {0}" : "Disable feature: {0}", feature);
      } else {
        DebugEx.Warning("Unknown feature name: {0}", feature);
      }
    }
    return true;
  }
}

}
