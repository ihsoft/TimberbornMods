// Timberborn Mod: Dual District Storage
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.DualDistrictStorage;

sealed record AsymmetricDualDistrictStoragePlacerSpec : ComponentSpec {
  [Serialize]
  public string NarrowTemplateName { get; init; }

  [Serialize]
  public string WideTemplateName { get; init; }
}
