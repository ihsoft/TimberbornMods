namespace UnityDev.Utils.LogUtilsLite;

static class DebugEx {
  public enum LogLevel {
    Info,
    Finer,
  }

  public static LogLevel VerbosityLevel { get; set; }

  public static void Info(string format, params object[] args) {
  }

  public static void Fine(string format, params object[] args) {
  }

  public static void Error(string format, params object[] args) {
  }
}
