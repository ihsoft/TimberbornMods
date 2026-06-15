using System;
using System.Collections.Generic;

namespace HarmonyLib;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HarmonyPatch : Attribute {
  public HarmonyPatch(Type type, string methodName) {
  }
}

public sealed class Harmony {
  static readonly HashSet<string> PatchedIds = new();

  readonly string _id;

  public Harmony(string id) {
    _id = id;
  }

  public static bool HasAnyPatches(string id) {
    return PatchedIds.Contains(id);
  }

  public static void Reset() {
    PatchedIds.Clear();
  }

  public PatchClassProcessor CreateClassProcessor(Type type) {
    return new PatchClassProcessor(_id, type);
  }

  internal static void AddPatch(string id) {
    PatchedIds.Add(id);
  }

  internal static void RemovePatch(string id) {
    PatchedIds.Remove(id);
  }
}

public sealed class PatchClassProcessor {
  readonly string _id;

  public Type Type { get; }
  public bool IsPatched { get; private set; }
  public bool IsUnpatched { get; private set; }

  public PatchClassProcessor(string id, Type type) {
    _id = id;
    Type = type;
  }

  public void Patch() {
    IsPatched = true;
    Harmony.AddPatch(_id);
  }

  public void Unpatch() {
    IsUnpatched = true;
    Harmony.RemovePatch(_id);
  }
}
