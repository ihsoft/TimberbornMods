// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.IO;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.PrefabSystem;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.LogUtilsLite {

/// <summary>A light version of logging utils from UnityDev specialized for Timberborn.</summary>
/// <remarks>https://github.com/ihsoft/UnityDev_Utils</remarks>
/// <seealso cref="LoggingSettings"/>
static class DebugEx {
  /// <summary>
  /// Logs a formatted INFO message giving a better context on the objects in the parameters.
  /// </summary>
  /// <remarks>
  /// The arguments are not just transformed into the strings by using their <c>ToString</c> method. Instead, this
  /// method tries to make a best guess of what the object is, and gives more context when possible.
  /// </remarks>
  /// <param name="format">The format string for the log message.</param>
  /// <param name="args">The arguments for the format string.</param>
  /// <seealso cref="ObjectToString"/>
  /// <seealso cref="Log"/>
  public static void Info(string format, params object[] args) {
    Log(LogType.Log, format, args);
  }

  /// <summary>Logs a formatted INFO message when the <i>verbose</i> logging mode is enabled.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Fine(string format, params object[] args) {
    if (LoggingSettings.VerbosityLevel > 0) {
      Log(LogType.Log, format, args);
    }
  }

  /// <summary>Logs a formatted WARNING message with a host identifier.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Warning(string format, params object[] args) {
    Log(LogType.Warning, format, args);
  }

  /// <summary>Logs a formatted ERROR message with a host identifier.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Error(string format, params object[] args) {
    Log(LogType.Error, format, args);
  }

  /// <summary>Generic method to emit a log record.</summary>
  /// <remarks>
  /// It also catches the improperly declared formatting strings, and reports the error instead of throwing.
  /// </remarks>
  /// <param name="type">The type of the log record.</param>
  /// <param name="format">The format string for the log message.</param>
  /// <param name="args">The arguments for the format string.</param>
  /// <seealso cref="ObjectToString"/>
  public static void Log(LogType type, string format, params object[] args) {
    try {
      Debug.unityLogger.LogFormat(type, format, args.Select(ObjectToString).ToArray());
    } catch (Exception e) {
      Debug.LogErrorFormat("Failed to format logging string: {0}.\n{1}", format, e.StackTrace);
    }
  }

  /// <summary>Helper method to make a user friendly object name for the logs.</summary>
  /// <remarks>
  /// This method is much more intelligent than a regular <c>ToString()</c>, it can detect some common types and give
  /// more context on them while keeping the output short.
  /// </remarks>
  /// <param name="obj">The object to stringify. It can be <c>null</c>.</param>
  /// <returns>A human friendly string or the original object.</returns>
  public static object ObjectToString(object obj) {
    if (obj == null) {
      return "[NULL]";
    }
    if (obj is string || obj.GetType().IsPrimitive) {
      return obj;  // Skip types don't override ToString() and don't have special representation.
    }
    return obj switch {
        BaseComponent baseComponent => BaseComponentToString(baseComponent),
        Component componentHost => $"[{componentHost.GetType().Name}]",
        Vector3 vec => $"[Vector3:{vec.x:0.0###},{vec.y:0.0###},{vec.z:0.0###}]",
        Quaternion rot => $"[Quaternion:{rot.x:0.0###}, {rot.y:0.0###}, {rot.z:0.0###}, {rot.w:0.0###}]",
        _ => obj.ToString()
    };
  }

  /// <summary>Helper method to make a user friendly object name for the logs.</summary>
  public static string BaseComponentToString(BaseComponent component) {
    var prefab = component.GetComponentFast<Prefab>();
    var blockObj = component.GetComponentFast<BlockObject>();
    if (prefab != null && blockObj != null) {
      return $"[{prefab.Name}@{blockObj.Coordinates}]";
    }
    if (prefab != null) {
      return $"[Prefab:{prefab.Name}]";
    }
    if (blockObj != null) {
      return $"[BlockObject@{blockObj.Coordinates}]";
    }
    return $"[{component.GetType().Name}]";
  }

  /// <summary>Lightweight version of the full log settings.</summary>
  /// <remarks>
  /// Only supports verbose level setting. To enable verbose logging, create an empty file "UnityDev_verboselogging" in
  /// the folder where the parent assembly file lives.
  /// </remarks>
  public static class LoggingSettings {
    const string LogLevelVerbosityFile = "UnityDev_verboselogging";

    public static int VerbosityLevel;

    static LoggingSettings() {
      var assembly = typeof(DebugEx).Assembly;
      var settingsFile = Path.Combine(Path.GetDirectoryName(assembly.Location)!, LogLevelVerbosityFile);
      VerbosityLevel = File.Exists(settingsFile) ? 5 : 0;
      if (VerbosityLevel > 0) {
        Info("Verbose logging level 5 is enabled for: {0}", assembly.FullName);
      }
    }
  }
}

}
