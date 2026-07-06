// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportRouteDistanceCacheKey : IEquatable<TransportRouteDistanceCacheKey> {
  readonly Guid _sourceId;
  readonly Guid _targetId;

  public TransportRouteDistanceCacheKey(Guid sourceId, Guid targetId) {
    _sourceId = sourceId;
    _targetId = targetId;
  }

  public bool Equals(TransportRouteDistanceCacheKey other) {
    return _sourceId.Equals(other._sourceId) && _targetId.Equals(other._targetId);
  }

  public override bool Equals(object obj) {
    return obj is TransportRouteDistanceCacheKey other && Equals(other);
  }

  public override int GetHashCode() {
    return HashCode.Combine(_sourceId, _targetId);
  }
}
