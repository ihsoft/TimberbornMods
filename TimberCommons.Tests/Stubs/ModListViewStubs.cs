using System;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyLib {
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
  public sealed class HarmonyPatch : Attribute {
    public HarmonyPatch(Type type, string methodName) {
    }
  }
}

namespace UnityEngine.UIElements {
  public enum DisplayStyle {
    Flex,
    None,
  }

  public sealed class Style {
    public DisplayStyle display { get; set; }
  }

  public class VisualElement {
    readonly List<VisualElement> _children = [];
    readonly List<string> _classes = [];

    public string name { get; set; }
    public VisualElement parent { get; private set; }
    public Style style { get; } = new();

    public void Add(VisualElement child) {
      child.parent = this;
      _children.Add(child);
    }

    public void Insert(int index, VisualElement child) {
      child.parent = this;
      _children.Insert(index, child);
    }

    public int IndexOf(VisualElement child) {
      return _children.IndexOf(child);
    }

    public T Q<T>(string elementName) where T : VisualElement {
      return DescendantsAndSelf().OfType<T>().FirstOrDefault(element => element.name == elementName);
    }

    public IEnumerable<string> GetClasses() {
      return _classes;
    }

    public void AddToClassList(string className) {
      _classes.Add(className);
    }

    IEnumerable<VisualElement> DescendantsAndSelf() {
      yield return this;
      foreach (var child in _children) {
        foreach (var descendant in child.DescendantsAndSelf()) {
          yield return descendant;
        }
      }
    }
  }

  public sealed class Button : VisualElement {
    public string text { get; set; }
    public event Action clicked;

    public void Click() {
      clicked?.Invoke();
    }
  }
}

namespace Timberborn.Modding {
  public sealed class Mod {
  }

  public static class ModPlayerPrefsHelper {
    static readonly HashSet<Mod> EnabledMods = [];

    public static bool IsModEnabled(Mod mod) {
      return EnabledMods.Contains(mod);
    }

    public static void SetEnabled(Mod mod, bool enabled) {
      if (enabled) {
        EnabledMods.Add(mod);
      } else {
        EnabledMods.Remove(mod);
      }
    }

    public static void Reset() {
      EnabledMods.Clear();
    }
  }
}

namespace Timberborn.ModdingUI {
  using Timberborn.Modding;
  using UnityEngine.UIElements;

  public sealed class ModItem {
    public ModItem(VisualElement root) {
      Root = root;
    }

    public VisualElement Root { get; }
  }

  public sealed class ModListView {
    public readonly Dictionary<Mod, ModItem> _modItems = [];

    public void Initialize(VisualElement root, IEnumerable<Mod> mods) {
    }
  }
}
