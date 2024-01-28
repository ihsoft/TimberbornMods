// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>Effect that is applied or removed based of the <see cref="IrrigationTower.IsIrrigating"/> state.</summary>
/// <remarks>
/// The same component can have multiple effects. The tower implementation decides which effects to activate based on
/// its internal logic.
/// </remarks>
public interface IRangeEffect {
  /// <summary>The effect identifier. It's used by the tower logic to identify the right effect.</summary>
  /// <remarks>
  /// Multiple effects can have the same name. In this case, all the matching effects wil be considered as a group.
  /// </remarks>
  /// <value>A string or <c>null</c>.</value>
  public string EffectGroup { get; }

  /// <summary>Applies affect to the specified tiles.</summary>
  /// <param name="tiles">The tiles to apply effect to.</param>
  public void ApplyEffect(IEnumerable<Vector2Int> tiles);

  /// <summary>Resets all effects that were applied in the last call to <see cref="ApplyEffect"/></summary>
  public void ResetEffect();
}

}
