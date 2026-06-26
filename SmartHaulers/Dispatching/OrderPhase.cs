// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

enum OrderPhase {
  Queued,
  Estimated,
  Deferred,
  Dispatchable,
  Covered,
  PickingUp,
  Delivering,
}
