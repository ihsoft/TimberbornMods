using UnityEngine;

namespace Timberborn.Coordinates;

enum Orientation {
  Cw0,
}

readonly record struct PickedCoordinates(Vector3Int Coordinates, float z, int Offset, bool CanAttachToSide);
