// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Concurrent;
using System.Threading;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Logging {

/// <summary>Helper to catch logs from the non-main threads and spitting them out to the main logger.</summary>
/// <remarks>Bind this class via Bindito as singleton to make it working.</remarks>
public class ThreadedLogsRecorder : ILoadableSingleton, ITickableSingleton {
  readonly ConcurrentQueue<string> _logRecords = new();
  Thread _mainUnityThread;

  public void Load() {
    _mainUnityThread = Thread.CurrentThread;
    Application.logMessageReceivedThreaded -= HandleThreadLog;
    Application.logMessageReceivedThreaded += HandleThreadLog;
  }

  public void Tick() {
    if (_logRecords.IsEmpty) {
      return;
    }
    var logs = _logRecords.ToArray();
    foreach (var t in logs) {
      DebugEx.Warning(t);
    }
  }

  void HandleThreadLog(string logString, string stackTrace, LogType type) {
    if (Thread.CurrentThread == _mainUnityThread) {
      return;
    }
    _logRecords.Enqueue($"[{type}] [Thread#{Thread.CurrentThread.ManagedThreadId}] {logString}");
  }
}

}
