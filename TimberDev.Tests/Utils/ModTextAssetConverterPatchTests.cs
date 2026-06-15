using System.IO;
using System.Reflection;
using Timberborn.ModdingAssets;
using UnityEngine;

namespace TimberDev.Tests;

static class ModTextAssetConverterPatchTests {
  public static void AppendsSourcePathForValidExtension() {
    var fileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), "enUS.txt"));
    var converter = new ModTextAssetConverter {
        ValidExtensions = [".txt"],
    };
    var asset = new TextAsset {
        name = "enUS",
    };

    InvokePostfix(fileInfo, converter, ref asset);

    Assert.Equal("enUS_at_" + fileInfo.FullName, asset.name);
  }

  public static void IgnoresInvalidExtension() {
    var fileInfo = new FileInfo(Path.Combine(Path.GetTempPath(), "enUS.csv"));
    var converter = new ModTextAssetConverter {
        ValidExtensions = [".txt"],
    };
    var asset = new TextAsset {
        name = "enUS",
    };

    InvokePostfix(fileInfo, converter, ref asset);

    Assert.Equal("enUS", asset.name);
  }

  static void InvokePostfix(FileInfo fileInfo, ModTextAssetConverter converter, ref TextAsset asset) {
    var patchType = typeof(IgorZ.TimberDev.Utils.ModTextAssetConverterPatch);
    var postfix = patchType.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
    var args = new object[] { fileInfo, converter, asset };
    postfix.Invoke(null, args);
    asset = (TextAsset)args[2];
  }
}
