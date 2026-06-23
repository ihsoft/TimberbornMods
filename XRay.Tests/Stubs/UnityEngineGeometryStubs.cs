namespace UnityEngine;

readonly record struct Ray;

readonly record struct Vector2Int(int x, int y);

readonly record struct Vector3(float x, float y, float z);

readonly record struct Vector3Int(int x, int y, int z) {
  public static Vector3Int operator +(Vector3Int a, Vector3Int b) {
    return new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
  }
}
