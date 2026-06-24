// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class DispatchCenterRegistry {
  readonly List<HaulerDispatchCenter> _dispatchCenters = [];

  public IReadOnlyList<HaulerDispatchCenter> DispatchCenters => _dispatchCenters;

  public void Register(HaulerDispatchCenter dispatchCenter) {
    if (!_dispatchCenters.Contains(dispatchCenter)) {
      _dispatchCenters.Add(dispatchCenter);
    }
  }

  public void Unregister(HaulerDispatchCenter dispatchCenter) {
    _dispatchCenters.Remove(dispatchCenter);
  }
}
