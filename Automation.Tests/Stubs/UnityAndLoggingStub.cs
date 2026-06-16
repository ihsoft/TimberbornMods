using System.Collections;
using System.Collections.Generic;

namespace UnityEngine {
  public sealed class Coroutine {
  }

  public sealed class GameObject {
    public string Name { get; }

    public GameObject(string name) {
      Name = name;
    }

    public T AddComponent<T>() where T : new() {
      return new T();
    }
  }

  public class MonoBehaviour {
    static readonly List<IEnumerator> QueuedCoroutines = [];

    public Coroutine StartCoroutine(IEnumerator enumerator) {
      QueuedCoroutines.Add(enumerator);
      return new Coroutine();
    }

    public static void RunQueuedCoroutines() {
      while (QueuedCoroutines.Count > 0) {
        var coroutines = QueuedCoroutines.ToArray();
        QueuedCoroutines.Clear();
        foreach (var coroutine in coroutines) {
          while (coroutine.MoveNext()) {
          }
        }
      }
    }
  }

  public class Sprite {
  }

  public sealed class WaitForEndOfFrame {
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
