# DualDistrictPile Operational Knowledge

## Purpose

Define the local architecture, generated assets, placement, preview, visualization, and validation contracts for the
Dual-District Pile inside DualDistrictStorage.

Repository-wide Buildings, Timbermesh, Icon, C#, Harmony, DI, and package-validation rules still apply. This document
owns exact pile identities and implementation relationships only; it does not generalize asymmetric placement to other
buildings.

## Tool And Physical Template Ownership

The faction-visible 3x3 tool blueprints are `DualDistrictPile.Folktails` and `DualDistrictPile.IronTeeth`. Each carries
`AsymmetricDualDistrictStoragePlacerSpec` and names its faction's hidden `DualDistrictPile.Narrow.*` and
`DualDistrictPile.Wide.*` templates. Only the tool blueprints belong in the faction tool collections.

The hidden narrow and wide templates are the linked physical entities with logical 3x1 and 3x2 footprints. Keep their
complete `BuildingSpec` companion data, including a `PlaceableBlockObjectSpec` with an empty `ToolGroupId` and
`DevModeTool=true`, so current global building consumers can inspect them without exposing them as player tools or
ordinary achievement structures.

`AsymmetricDualDistrictStoragePlacer` creates the narrow entity at the selected placement. It derives the wide entity
from the orientation-relative `(2, 2, 0)` offset, flips its orientation, preserves the requested finished/unfinished
placement state, and links both `LinkedBuilding` participants. Do not replace this with stock `Layout=Half`, which does
not express unequal logical footprints.

The stock `BuildingPlacer` also accepts ordinary `BuildingSpec` templates. `BuildingPlacerPatch` must exclude only
templates carrying `AsymmetricDualDistrictStoragePlacerSpec`, leaving the custom `IBlockObjectPlacer` as their sole
owner. Register the complete repository patch set through the one atomic `HarmonyPatcher.ApplyPatch` call and one patch
ID; do not register this patch separately under the same ID.

## Preview Ownership

The 3x3 tool preview is not an instance of the narrow and wide physical entities. Its ordinary entrance marker therefore
owns only the first entrance.

`AsymmetricDualDistrictStoragePlacementMarker` is both the decorator component for
`AsymmetricDualDistrictStoragePlacerSpec` and the preview-only second-entrance owner. Keep its concrete transient Bindito
binding and decorator mapping. It derives the opposite marker from `PositionedEntrance`, the current orientation, and
the local `(0, 4, 0)` offset rather than reading the hidden placed templates.

Validate the full 3x3 preview silhouette and both entrance arrows under every supported rotation. Correct linked
placement does not establish correct preview affordances.

## Atomic Generated Pile Models

One invocation of `Tools/create_dual_district_pile_models.py` owns this complete Z = 1.5 clipped Timbermesh set:

- `LargePile.Folktails.Model` -> `DualDistrictPile.Folktails.Model`;
- `LargePile.Folktails.ConstructionStage0.Model` ->
  `DualDistrictPile.Folktails.ConstructionStage0.Model`;
- `LargeIndustrialPile.IronTeeth.Model` -> `DualDistrictPile.IronTeeth.Model`;
- `LargeIndustrialPile.IronTeeth.ConstructionStage0.Model` ->
  `DualDistrictPile.IronTeeth.ConstructionStage0.Model`;
- `ConstructionBase3x3.Model` -> `DualDistrictPile.ConstructionBase.Model`.

All five outputs live below `Mod/Buildings/Storage/DualDistrictPile/` with matching `.timbermesh` filenames. The script
uses the current game archive, UnityPy, and official Timbermesh protobuf schema, requires one source node, validates and
round-trips every output, and writes the set under one output root. Regenerate, structurally validate, package,
hash-check, and review all five together; do not hand-edit or refresh only one member after a shared source or clipping
change.

The narrow and wide physical entities intentionally render the same 1.5-tile geometric half while retaining unequal
logical footprints. Opposite placement orientations compose the complete 3x3 finished and construction visuals.

Pile icons remain owned by the shared icon generator and the exact contracts in the parent `AGENTS.md`; do not create a
second pile-only icon pipeline.

## Visualization Ownership

Both physical inventories mirror the same logical amount. `StockpileGoodPileVisualizerPatch` apportions only the
rendered pile amount: the narrow entity uses `VisualizationShareNumerator=1`, the wide entity uses 2, and both use
`VisualizationShareDenominator=3`.

The narrow entity owns the shared dirt-plane visualization. The wide entity sets `OwnsSharedPlaneVisualization=false`,
and `StockpilePlaneVisualizerPatch` clears that redundant plane after initialization. Do not interpret visual shares or
plane ownership as inventory-capacity division.

Every mutually exclusive construction state keeps an active collider: the initial construction base, stage 0, and the
finished state must each remain selectable when it is the visible child. Construction-mode selection is not sufficient
evidence because occupied-block lookup can mask a missing collider.

## Validation

After changing pile blueprints, placement, preview, generated assets, colliders, or visualization:

1. Regenerate and validate the complete five-model set when its inputs or transformation changed; regenerate the shared
   icon set when a pile icon input or composition changed.
2. Verify the visible faction tool identities, hidden template references, complete hidden companion specs, and absence
   of hidden tools from ordinary player selection.
3. Build through the ordinary package path and verify the DLL, blueprints, Timbermesh models, icons, and metadata match
   their tracked sources.
4. In the real game, rotate the preview and confirm the full silhouette and both entrance markers, then place and build
   both factions through the ordinary unfinished path.
5. At zero progress, stage 0, and completion, verify exactly one intended visual state, normal-view selection, opposite
   entrances, linking, and a clean log.
6. Verify selected-good synchronization during construction, completed inventory replication, 1/3 and 2/3 pile
   visualization, and single dirt-plane ownership.

The tool, preview, ordinary construction, active-state selection, completed linked behavior, and unfinished
selected-good synchronization have been confirmed in the real game. Do not claim save/load, broader logistics,
reservation, or upgrade compatibility beyond separately recorded evidence.
