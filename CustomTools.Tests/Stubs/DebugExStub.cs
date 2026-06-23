using System.Collections.Generic;

namespace UnityDev.Utils.LogUtilsLite;

public static class DebugEx {
  public static void Warning(string message, params object[] args) {
  }

  public static string C2S<T>(IEnumerable<T> values, string separator = ", ") {
    return string.Join(separator, values);
  }
}
