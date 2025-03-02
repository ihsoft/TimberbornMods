// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.AssetSystem;
using Timberborn.InputSystem;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Service to manage cursors from the game.</summary>
/// <remarks>
/// The difference from the stock game service is that the cursors don't need to be pre-configured via specs. The
/// missing specs will eb created on the fly from the texture assets.
/// </remarks>
public sealed class CustomCursorService(CursorService cursorService, IAssetLoader assetLoader) {

  const string BasePath = "UI/Cursors/";

  /// <summary>Sets the custom cursor.</summary>
  /// <remarks>
  /// If the spec is not known to the system, it will be created and registered. There should be textures at
  /// "UI/Cursor/" paths named: "&lt;cursorName&gt;-small" and "&lt;cursorName&gt;-large".
  /// </remarks>
  /// <param name="cursorName">Cursor spec Id.</param>
  public void SetCursor(string cursorName) {
    if (!cursorService._cursorSpecs.TryGetValue(cursorName, out var cursorSpec)) {
      cursorSpec = new CustomCursorSpec {
          Id = cursorName,
          SmallCursor = assetLoader.Load<Texture2D>(BasePath + cursorName + "-small"),
          LargeCursor = assetLoader.Load<Texture2D>(BasePath + cursorName + "-large"),
      };
    }
    SetCursor(cursorSpec);
  }

  /// <summary>Sets a custom cursor from the spec.</summary>
  public void SetCursor(CustomCursorSpec cursorSpec) {
    if (!cursorService._cursorSpecs.ContainsKey(cursorSpec.Id)) {
      cursorService._cursorSpecs.Add(cursorSpec.Id, cursorSpec);
    }
    cursorService.SetCursor(cursorSpec.Id);
  }

  /// <summary>Resets the cursor to the default one.</summary>
  public void ResetCursor() {
    cursorService.ResetCursor();
  }
}
