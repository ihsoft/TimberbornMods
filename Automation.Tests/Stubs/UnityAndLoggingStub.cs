namespace UnityEngine {
  public class MonoBehaviour {
  }

  public static class Mathf {
    public static int RoundToInt(float value) {
      return (int)System.MathF.Round(value);
    }
  }
}

namespace UnityDev.Utils.LogUtilsLite {
  public static class DebugEx {
    public static void Fine(string format, params object[] args) {
    }

    public static void Info(string format, params object[] args) {
    }

    public static void Warning(string format, params object[] args) {
    }

    public static void Error(string format, params object[] args) {
    }

    public static string ObjectToString(object obj) {
      return obj?.ToString() ?? "";
    }
  }

  public static class HostedDebugLog {
    public static void Fine(object host, string format, params object[] args) {
    }

    public static void Info(object host, string format, params object[] args) {
    }

    public static void Error(object host, string format, params object[] args) {
    }
  }
}
