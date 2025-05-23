// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ProtoBuf;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService;

/// <summary>Definition of the moisture override.</summary>
/// <seealso cref="SoilOverridesService"/>
[ProtoContract]
public readonly record struct MoistureOverride {
  /// <summary>Definition of the moisture override.</summary>
  public MoistureOverride(Vector3Int Coordinates, float MoistureLevel, float DesertLevel) {
    this.Coordinates = Coordinates;
    this.MoistureLevel = MoistureLevel;
    this.DesertLevel = DesertLevel;
  }

  /// <summary>Coordinates of the tile.</summary>
  [ProtoMember(1)]
  public Vector3Int Coordinates { get; }

  /// <summary>Moisture level of the tile.</summary>
  [ProtoMember(2)]
  public float MoistureLevel { get; }

  /// <summary>Desert level of the tile.</summary>
  [ProtoMember(3)]
  public float DesertLevel { get; }
}
