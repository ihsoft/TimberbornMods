using System.Collections.Generic;

namespace UnityEngine;

static class Debug {
  /// <summary>Messages emitted by the copied API helper when the optional endpoint is absent.</summary>
  public static readonly List<string> Messages = [];

  /// <summary>Captures formatted Unity INFO messages for assertions.</summary>
  public static void LogFormat(string format, params object[] args) {
    Messages.Add(string.Format(format, args));
  }
}
