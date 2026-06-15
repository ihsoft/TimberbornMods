using System.IO;
using UnityEngine;

namespace Timberborn.ModdingAssets;

public sealed class ModTextAssetConverter {
  public string[] ValidExtensions { get; set; } = [];

  public bool TryConvert(FileInfo fileInfo, out TextAsset asset) {
    asset = null;
    return false;
  }
}
