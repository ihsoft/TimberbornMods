// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TimberApi.DependencyContainerSystem;
using TimberApi.ModSystem;
using Timberborn.AssetSystem;
using Timberborn.InputSystem;
using Timberborn.StatusSystem;
using UnityDev.Utils.LogUtils;
using UnityDev.Utils.Reflections;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable InconsistentNaming
namespace CustomResources {

/// <summary>Installs patches that allows passing custom resource path to the game stock methods.</summary>
/// <remarks>
/// <p>
/// It's not supported globally. Only particular methods are patched:
/// <ul>
/// <li>
/// <see cref="StatusToggle"/>. Set a full resource name as <c>spriteName</c> when creating a status with icon.
/// </li>
/// <li>
/// <see cref="CursorService"/>. Set a full path to a texture in method <see cref="CursorService.SetCursor"/>. The
/// cursor will be created from this texture. The texture must be of type <c>Cursor</c> an allow read/write access.
/// </li>
/// </ul>
/// </p>
/// <p>
/// This patcher can be either be a dependency to your Timberborn mod via Mod.io, or you can simply add it into your
/// project as a source. In the latter case, call <see cref="AssetPatcher.Patch"/> from any of the in-game
/// configurators.
/// </p>
/// </remarks>
public static class AssetPatcher {
  const string AssetPatcherId = "IgorZ.CustomResources.v";
  const int Version = 1;

  /// <summary>List of unique asset prefixes in lower case.</summary>
  /// <remarks>It loads lazily on first access.</remarks>
  static List<string> AssetPrefixes => _assetPrefixes ??= DependencyContainer.GetInstance<IModRepository>()
      .All()
      .SelectMany(r => r.Assets)
      .Select(a => a.Prefix.ToLower())
      .ToHashSet()
      .ToList();
  static List<string> _assetPrefixes;

  /// <summary>Resource asset loader that is used for all resources in the patch(es).</summary>
  /// <remarks>It loads lazily on first access.</remarks>
  static IResourceAssetLoader ResourceAssetLoader =>
      _resourceAssetLoader ??= DependencyContainer.GetInstance<IResourceAssetLoader>();
  static IResourceAssetLoader _resourceAssetLoader;

  /// <summary>Applies the patches to the applicable classes.</summary>
  /// <remarks>
  /// It's safe to call this method multiple times from the same or the different mods. The implementation will deal
  /// with the possible duplicates or version upgrades.
  /// </remarks>
  public static void Patch() {
    // Upgrade the patch version if needed.
    var installedVersionId = Harmony
        .GetAllPatchedMethods()
        .Select(Harmony.GetPatchInfo)
        .SelectMany(i => i.Owners)
        .FirstOrDefault(p => p.StartsWith(AssetPatcherId));
    if (installedVersionId != null) {
      var version = int.Parse(installedVersionId.Substring(AssetPatcherId.Length));
      switch (version) {
        case Version:
          DebugEx.Info("Custom resources patch v{0} is already applied. Skipping patches from: {1}",
                       version, Assembly.GetExecutingAssembly());
          return;
        case > Version:
          DebugEx.Info("A better version v{0} of custom resources patch is already applied. Skipping patches from: {1}",
                       version, Assembly.GetExecutingAssembly());
          return;
        default:
          DebugEx.Info("Discarding the old version of custom resources patch: v{0}", version);
          Harmony.UnpatchID(AssetPatcherId + version);
          break;
      }
    }
    var harmony = new Harmony(AssetPatcherId + Version);
    harmony.PatchAll(typeof(StatusSpriteLoaderPatch));
    harmony.PatchAll(typeof(CursorServicePatch));
    DebugEx.Info("Custom resources patch v{0} applied from: {1}", Version, Assembly.GetExecutingAssembly());
  }

  /// <summary>Loads the requested resource if <paramref name="resourceName"/> is a path to a mod resource.</summary>
  /// <remarks>
  /// This method does nothing if the <paramref name="resourceName"/> doesn't start from a <i>known</i> mod asset
  /// prefix. If the name starts from a known prefix (i.e. the one that was declared by any mod in the game), then this
  /// resource is attempted to be loaded, assuming that <paramref name="resourceName"/> is a full path in the mods asset
  /// scope. On success, <c>AbortPrefixesException</c> is raised to abort the further prefixes on the patch to execute.
  /// The patches <i>must</i> implement finalizers to handle this exception and take the result from <c>__state</c>.
  /// </remarks>
  /// <param name="resourceName">
  /// The name in any format. If it starts from a mod asset prefix, than this resource will be loaded.
  /// </param>
  /// <param name="result">
  /// The loaded custom resource or <c>null</c> if <paramref name="resourceName"/> doesn't specify a resource to any
  /// known mod in the game.
  /// </param>
  /// <typeparam name="T">type of the resource to load from <c>IResourceAssetLoader</c>.</typeparam>
  /// <exception cref="AbortPrefixesException">if the custom resource was loaded</exception>
  static void MaybeLoadCustomResource<T>(string resourceName, out T result) where T : Object {
    var checkName = resourceName.ToLower();
    if (AssetPrefixes.Any(prefix => checkName.StartsWith(prefix))) {
      result = ResourceAssetLoader.Load<T>(resourceName);
      throw new AbortPrefixesException();  // Prevent any other prefixes to execute.
    }
    result = null;
  }

  /// <summary>
  /// Thrown when a custom resource was detected and successfully loaded from <see cref="MaybeLoadCustomResource{T}"/>
  /// </summary>
  class AbortPrefixesException : Exception {}

  /// <summary>Custom status icons support.</summary>
  /// <remarks>Simply provide a full path to the mod's custom sprite as "spriteName" to <c>StatusToggle</c>.</remarks>
  [HarmonyPatch(typeof(StatusSpriteLoader), nameof(StatusSpriteLoader.LoadSprite))]
  public static class StatusSpriteLoaderPatch {
    [HarmonyPriority(Priority.First)]
    static void Prefix(string spriteName, out Sprite __state) {
      MaybeLoadCustomResource(spriteName, out __state);
    }

    [HarmonyPriority(Priority.First)]
    static Exception Finalizer(Exception __exception, ref Sprite __result, Sprite __state) {
      if (__exception is AbortPrefixesException) {
        __result = __state;  // Catch the result, loaded in the prefix.
        return null;
      }
      return __exception;
    }
  }

  /// <summary>Custom cursors support.</summary>
  /// <remarks>
  /// Simply provide a full path to the mod's custom sprite as "cursorName" to <c>CursorService.SetCursor</c>. The
  /// resolution of the sprite will be the size of the cursor. The game uses size 64x64.
  /// </remarks>
  [HarmonyPatch(typeof(CursorService), "GetCursor", typeof(string))]
  public static class CursorServicePatch {
    const string CustomCursorTypeName = "Timberborn.InputSystem.CustomCursor";
    static Type _customCursorType;
    static ReflectedField<Texture2D> _customCursorSmallCursorField;
    static ReflectedField<Texture2D> _customCursorLargeCursorField;

    [HarmonyPriority(Priority.First)]
    static void Prefix(string cursorName, out Object __state) {
      var checkName = cursorName.ToLower();
      if (AssetPrefixes.Any(prefix => checkName.StartsWith(prefix))) {
        if (_customCursorType == null) {
          MakeReflections();
        }
        var texture = ResourceAssetLoader.Load<Texture2D>(cursorName);
        __state = ScriptableObject.CreateInstance(_customCursorType);
        _customCursorSmallCursorField.Set(__state, texture);
        _customCursorLargeCursorField.Set(__state, texture);
        throw new AbortPrefixesException();  // Prevent any other prefixes to execute.
      }
      __state = null;
    }
    [HarmonyPriority(Priority.First)]
    static Exception Finalizer(Exception __exception, ref Object __result, Object __state) {
      if (__exception is AbortPrefixesException) {
        __result = __state;  // Catch the result, loaded in the prefix.
        return null;
      }
      return __exception;
    }

    static void MakeReflections() {
      _customCursorType = typeof(CursorService).Assembly.GetType(CustomCursorTypeName);
      if (_customCursorType == null) {
        throw new InvalidOperationException("Cannot find type: " + CustomCursorTypeName);
      }
      _customCursorSmallCursorField = new ReflectedField<Texture2D>(_customCursorType, "_smallCursor");
      _customCursorLargeCursorField = new ReflectedField<Texture2D>(_customCursorType, "_largeCursor");
      if (!_customCursorLargeCursorField.IsValid() || !_customCursorSmallCursorField.IsValid()) {
        throw new InvalidOperationException("Cannot get reflections on: " + CustomCursorTypeName);
      }
    }
  }
}

}
