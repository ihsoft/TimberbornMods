// Timberborn Mod: Dual District Storage
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.DualDistrictStorage;

// Marks persistent physical halves created only by AsymmetricDualDistrictStoragePlacer. They remain building
// templates for construction and save/load, but must not enter the player tool pipeline.
sealed record AsymmetricDualDistrictStoragePartSpec : ComponentSpec;
