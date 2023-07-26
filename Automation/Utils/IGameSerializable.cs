// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Persistence;

namespace Automation.Utils {

/// <summary>Base interface for the game object that can save/load game state.</summary>
public interface IGameSerializable {
  /// <summary>Loads state from the loader.</summary>
  void LoadFrom(IObjectLoader objectLoader);

  /// <summary>Saves state to the saver.</summary>
  void SaveTo(IObjectSaver objectSaver);
}

}
