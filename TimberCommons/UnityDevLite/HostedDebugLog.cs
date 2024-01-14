// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System.Diagnostics.CodeAnalysis;
using Timberborn.BaseComponentSystem;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.LogUtilsLite {

/// <summary>A light version of logging utils from UnityDev specialized for Timberborn.</summary>
/// <remarks>https://github.com/ihsoft/UnityDev_Utils</remarks>
/// <seealso cref="DebugEx.LoggingSettings"/>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
static class HostedDebugLog {
  /// <summary>Logs a formatted INFO message with a host identifier.</summary>
  public static void Info(BaseComponent host, string format, params object[] args) {
    Log(LogType.Log, host, format, args);
  }

  /// <summary>
  /// Logs a formatted INFO message with a host identifier when the <i>verbose</i> logging mode is enabled.
  /// </summary>
  public static void Fine(BaseComponent host, string format, params object[] args) {
    if (DebugEx.LoggingSettings.VerbosityLevel > 0) {
      Log(LogType.Log, host, format, args);
    }
  }

  /// <summary>Logs a formatted WARNING message with a host identifier.</summary>
  public static void Warning(BaseComponent host, string format, params object[] args) {
    Log(LogType.Warning, host, format, args);
  }

  /// <summary>Logs a formatted ERROR message with a host identifier.</summary>
  public static void Error(BaseComponent host, string format, params object[] args) {
    Log(LogType.Error, host, format, args);
  }

  /// <summary>Generic method to emit a hosted log record.</summary>
  /// <param name="type">The type of the log record.</param>
  /// <param name="host">The host object which is bound to the log record. It can be <c>null</c>.</param>
  /// <param name="format">The format string for the log message.</param>
  /// <param name="args">The arguments for the format string.</param>
  /// <seealso cref="DebugEx.ObjectToString"/>
  public static void Log(LogType type, object host, string format, params object[] args) {
    DebugEx.Log(type, DebugEx.ObjectToString(host) + " " + format, args);
  }
}

}  // namespace
