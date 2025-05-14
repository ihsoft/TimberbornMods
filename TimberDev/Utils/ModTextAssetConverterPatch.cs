// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO;
using System.Linq;
using HarmonyLib;
using Timberborn.ModdingAssets;
using UnityEngine;

namespace IgorZ.TimberDev.Utils;

/// <summary>Install this patch to improve localization errors logs (there will be a filename).</summary>
/// <remarks>This patch must be applied from the "MainMenu" context.</remarks>
[HarmonyPatch(typeof(ModTextAssetConverter), nameof(ModTextAssetConverter.TryConvert))]
static class ModTextAssetConverterPatch {
  static void Postfix(FileInfo fileInfo, ModTextAssetConverter __instance, ref TextAsset asset) {
    if (__instance.ValidExtensions.Any(x => fileInfo.Name.EndsWith(x))) {
      asset.name = asset.name + "_at_" + fileInfo.FullName;
    }
  }
}
