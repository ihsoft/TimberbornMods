namespace UnityDev.Utils.LogUtilsLite;

static class DebugEx {
  public static void Fine(string format, params object[] args) {
  }

  public static object ObjectToString(object obj) {
    return obj?.ToString() ?? "[NULL]";
  }
}

static class HostedDebugLog {
  public static void Fine(object host, string format, params object[] args) {
  }
}
