// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.LogUtilsLite;

/// <summary>A light version of logging utils from UnityDev specialized for Timberborn.</summary>
/// <remarks>https://github.com/ihsoft/UnityDev_Utils</remarks>
static class DebugEx {

  /// <summary>The levels of log record.</summary>
  public enum LogLevel {
    None = -1,
    Error = 0,
    Warning = 1,
    Info = 2,
    Fine = 3,
    Finer = 4,
  }

  /// <summary>The current verbosity level for the logging.</summary>
  public static LogLevel VerbosityLevel = LogLevel.Info;

  /// <summary>
  /// Logs a formatted INFO message giving a better context on the objects in the parameters.
  /// </summary>
  /// <remarks>
  /// The arguments aren't transformed into the strings by using their <c>ToString</c> method. Instead, this
  /// method tries to make the best guess of what the object is, and gives more context when possible.
  /// </remarks>
  /// <param name="format">The format string for the log message.</param>
  /// <param name="args">The arguments for the format string.</param>
  /// <seealso cref="ObjectToString"/>
  /// <seealso cref="Log"/>
  public static void Info(string format, params object[] args) {
    if (VerbosityLevel >= LogLevel.Info) {
      Log(LogType.Log, format, args);
    }
  }

  /// <summary>Logs a formatted INFO message when the <i>verbose</i> logging mode is enabled.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Fine(string format, params object[] args) {
    if (VerbosityLevel >= LogLevel.Fine) {
      Log(LogType.Log, format, args);
    }
  }

  /// <summary>Logs a formatted WARNING message with a host identifier.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Warning(string format, params object[] args) {
    if (VerbosityLevel >= LogLevel.Warning) {
      Log(LogType.Warning, format, args);
    }
  }

  /// <summary>Logs a formatted ERROR message with a host identifier.</summary>
  /// <inheritdoc cref="Info"/>
  public static void Error(string format, params object[] args) {
    if (VerbosityLevel >= LogLevel.Error) {
      Log(LogType.Error, format, args);
    }
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
      var objects = args.Select(x => x switch {
            IList list => string.Join(",", list.Cast<object>().Select(ObjectToString)),
            _ => ObjectToString(x),
      }).ToArray();
      Debug.unityLogger.LogFormat(type, format, objects);
    } catch (Exception e) {
      Debug.LogErrorFormat("Failed to format logging string: {0}.\n{1}", format, e.StackTrace);
    }
  }

  /// <summary>Helper method to make a user-friendly object name for the logs.</summary>
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
    if (obj is Vector3 vec) {
      return $"[Vector3:{vec.x:0.0###},{vec.y:0.0###},{vec.z:0.0###}]";
    }
    if (obj is Quaternion rot) {
      return $"[Quaternion:{rot.x:0.0###}, {rot.y:0.0###}, {rot.z:0.0###}, {rot.w:0.0###}]";
    }
    if (obj is BaseComponent baseComponent) {
      return BaseComponentToString(baseComponent);
    }
    if (obj is not Component unityComponent) {
      return obj.ToString();
    }
    if (!unityComponent) {  // It is important to use the "!" notion to catch the destroyed objects!
      return "[DestroyedComponent]";
    }
    return $"[{unityComponent.GetType().Name}]";
  }

  /// <summary>Helper method to make a user-friendly object name for the logs.</summary>
  public static string BaseComponentToString(BaseComponent component) {
    if (component == null) {
      return $"[IncompleteComponent]";
    }
    if (!component) {
      // It is important to use the "!" notion to catch the destroyed GameObject!
      return "[DestroyedComponent]";
    }
    var blockObj = component.GetComponent<BlockObject>();
    if (blockObj) {
      // Block objects have coordinates. It's good to know them. 
      return $"[{component.Name}@{blockObj.Coordinates}]";
    }
    // It's just a Unity prefab object.
    return $"[Prefab:{component.Name}]";
  }

  /// <summary>Collection-to-string – it makes a comma separated string from the enumerable.</summary>
  public static string C2S(IEnumerable enumerable, string separator = ",") {
    return string.Join(separator, enumerable);
  }

  /// <summary>Collection-to-string – it makes a comma separated string from the enumerable.</summary>
  public static string C2S<T>(IEnumerable<T> enumerable, string separator = ",") {
    return string.Join(separator, enumerable);
  }
}