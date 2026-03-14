// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.Utils;

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
/// the line is ignored. The feature can have a value, to specify it, write it as a pair "name=value".
/// </p>
/// <code>
/// # Enables verbose logging.
/// !DebugEx.VerboseLogging
/// # Overrides PrefabOptimizer max registry size
/// PrefabOptimizer.MaxExpectedRegistrySize=500
/// </code>
/// <example>
/// <p>
/// The plugin should call <see cref="ReadFeatures"/> from some common place to initialize it's settings. E.g. it can be
/// done from the configurator. One way of simplifying the usage is declaring a static class <c>Features</c> that would
/// read the definition on the first use:
/// </p>
/// <code><![CDATA[
/// static class Features {
///   public bool MyFeatureFlag;
///   public int FeatureValue;
///
///   static Features() {
///     FeatureController.ReadFeatures(Consume);
///   }
/// 
///   static bool Consume(string name, bool enabled, string value) {
///     return name switch {
///         "FeatureFlag" => FeatureController.SetFlag(ref FeatureFlag, name, enabled, value),
///         "FeatureValue" => FeatureController.SetValue(ref FeatureValue, name, enabled, value),
///         _ => false
///     };
///   }
/// }
/// ]]></code>
/// </example>
/// </remarks>
public static class FeatureController {

  static readonly Regex FeatureNameCheck = new(@"^[.a-zA-Z0-9]+$");

  /// <summary>
  /// The name of file that holds the enabled features. It must be stored in the same folder as the plugin's DLL.
  /// </summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public const string FeaturesFilename = "TimberDev_Features.txt";

  /// <summary>Reads feature config file located at <see cref="FeaturesFilename"/>.</summary>
  /// <param name="basePath">The path to look for the features file at.</param>
  /// <param name="consumeFn">
  /// The function that takes the parsed feature name, it's state and an optional value. The function must return
  /// <c>true</c> if it recognized the feature name and accepted it.
  /// </param>
  /// <returns><c>false</c> if no file were found.</returns>
  /// <seealso cref="SetFlag"/>
  /// <seealso cref="SetValue{T}"/>
  public static bool ReadFeatures(string basePath, Func<string, bool, string, bool> consumeFn) {
    var featuresFile = Path.Combine(basePath, FeaturesFilename);
    if (!File.Exists(featuresFile)) {
      return false;
    }
    var features = File.ReadAllLines(featuresFile)
        .Select(x => x.Trim())
        .Where(x => x.Length > 0 && x[0] != '#')
        .ToList();
    DebugEx.Info("Loaded {0} feature definitions for {1} from {2}",
                 features.Count, typeof(FeatureController).Assembly.FullName, featuresFile);
    foreach (var feature in features) {
      var featureName = feature;
      var isEnabled = true;
      if (featureName[0] == '!') {
        isEnabled = false;
        featureName = featureName.Substring(1);
      }
      string featureValue = null;
      var parts = featureName.Split(new[] {'='}, 2);
      if (parts.Length > 1) {
        featureName = parts[0];
        featureValue = parts[1];
      }
      if (!FeatureNameCheck.IsMatch(featureName)) {
        DebugEx.Error("Bad feature name ignored: {0}", featureName);
        continue;
      }
      if (consumeFn(featureName, isEnabled, featureValue)) {
        DebugEx.Info(isEnabled ? "Enable feature: {0}" : "Disable feature: {0}", feature);
      } else {
        DebugEx.Error("Unrecognized feature: {0}", feature);
      }
    }
    return true;
  }

  /// <summary>Verifies the feature consistency and assigns the flag state.</summary>
  /// <param name="featureFlag">The variable to set to the feature state.</param>
  /// <param name="name">Name of the feature.</param>
  /// <param name="isEnabled">State of the feature.</param>
  /// <param name="value">
  /// Value of the feature. It will be checked to be <c>null</c> since the flag features don't have value.
  /// </param>
  /// <returns><c>true</c> if the flag was successfully processed.</returns>
  /// <exception cref="InvalidOperationException">if the value is not <c>null</c>.</exception>
  public static bool SetFlag(ref bool featureFlag, string name, bool isEnabled, object value) {
    if (value != null) {
      throw new InvalidOperationException("Value is not expected for feature: " + name);
    }
    featureFlag = isEnabled;
    return true;
  }
  
  /// <summary>Verifies the feature consistency and assigns the feature value.</summary>
  /// <param name="featureValue">The variable to set to the feature value.</param>
  /// <param name="name">Name of the feature.</param>
  /// <param name="isEnabled">State of the feature.</param>
  /// <param name="value">
  /// Value of the feature. It will be checked to be not <c>null</c> and attempted to convert to a value of type
  /// <typeparamref name="T"/>.
  /// </param>
  /// <returns><c>true</c> if the flag was successfully processed.</returns>
  /// <exception cref="InvalidOperationException">if the value is <c>null</c>.</exception>
  public static bool SetValue<T>(ref T featureValue, string name, bool isEnabled, object value) {
    if (value == null) {
      throw new InvalidOperationException("Feature must have a value: " + name);
    }
    T parsedValue;
    try {
      parsedValue = (T)Convert.ChangeType(value, typeof(T));
    } catch (Exception) {
      throw new InvalidOperationException($"Bad feature value: name={name}, value={value}, type={typeof(T)}");
    }
    if (isEnabled) {
      featureValue = parsedValue;
    }
    return true;
  }
}