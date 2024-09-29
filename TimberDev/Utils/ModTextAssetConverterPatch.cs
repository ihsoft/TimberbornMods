// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace IgorZ.SmartPower.Utils;

/// <summary>Install this patch to improve localization errors logs (there will be a file name).</summary>
/// <remarks>This patch must be applied from the "MainMenu" context.</remarks>
[HarmonyPatch]
static class ModTextAssetConverterPatch {
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.ModdingAssets.ModTextAssetConverter:TryConvert");
  }

  static void Postfix(FileInfo fileInfo, ref TextAsset asset) {
    if (fileInfo.Name.EndsWith(".csv")) {
      asset.name = asset.name + "_at_" + fileInfo.FullName;
    }
  }
}
