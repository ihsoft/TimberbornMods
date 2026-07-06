// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Navigation;

namespace IgorZ.SmartHaulers.Dispatching;

interface ITransportRouteDistanceProvider {
  bool TryFindRoute(Accessible start, Accessible end, out float distance);
}
