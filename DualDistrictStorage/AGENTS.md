# DualDistrictStorage Agent Instructions

These instructions apply to work under `DualDistrictStorage/`. Follow the repository-wide instructions and
Timberborn Timbermesh Operational Knowledge as well.

## Generated Model Ownership

`Tools/create_half_timbermesh.py` owns both tracked generated models as one atomic artifact set; do not hand-edit either
binary asset or regenerate, replace, validate, package, or review only one of them:

- `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.Model.timbermesh`, with resource
  identity `DualDistrictWarehouse.Folktails.Model`;
- `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.MirroredRoof.Model.timbermesh`, with
  resource identity `DualDistrictWarehouse.Folktails.MirroredRoof.Model`.

One generator invocation requires both output identities and both output paths. It reads the exact stock
`MediumWarehouse.Folktails.Model` object and clips the entrance-side half at Timbermesh Z = 1. It serializes the normal
output, then clones that result and derives the mirrored-roof output from the same clipped geometry. Its archive,
UnityPy, and official Timbermesh protobuf inputs are local external dependencies and must remain ignored.

The script is intentionally specialized and must fail if the source representation no longer matches its validated
one-node, single-payload expectations. For the mirrored variant, it requires exactly one
`IrregularPlanks_Mossy.Folktails` source submesh and rejects selected vertex indices shared with another material. It
changes only that roof's source `uv0` with `U' = 1 - U`, preserves V, and negates tangent direction and handedness. Both
outputs must retain identical geometry, material descriptors, and indices.

Revalidate the current game archive and official Timbermesh schema before adapting the generator to a changed game
representation. When the generator interface or either output identity changes, update both outputs, the blueprint
child references, and runtime activation contract in the same change.

## Current Model Contract

The current building uses two generated variants of the same entrance-side 3x1 geometry under one `#Finished` root:

- `#Finished/NormalModel` references
  `Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.Model`;
- `#Finished/MirroredRoofModel` references
  `Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.MirroredRoof.Model`.

The single `CollidersSpec` remains on `#Finished`; do not add separate colliders to the model children without evidence
that the building ownership model changed.

Every instance defaults to `NormalModel` during `Awake`. After linking, the existing `PrimaryHalf()` GUID ordering keeps
the primary half normal and activates `MirroredRoofModel` only on the secondary half. Both peers must compute the same
role, and a missing expected child identity must remain a diagnostic warning rather than a silent fallback.

Runtime mesh cloning, optimized-geometry selection, and atlas-fragment UV mutation are not part of the current design.
Do not reintroduce them merely because they were used by an earlier working implementation.

Both outputs remain loose Timbermesh package assets. The ordinary mod build copies them into the compatibility lane;
Unity export is not part of their ownership path.

The building geometry, opposite-entrance placement, generated variant activation, and seamless roof texture have been
confirmed in the real game. Broader logistics and save/load validation remain in progress; do not describe those
lifecycles as confirmed until they are tested.

## Generated Variant Validation

After changing the generator, either generated model, blueprint child identities, or role activation:

1. Regenerate both tracked Timbermesh outputs together through one owning-script invocation when their source
   transformation changed.
2. Verify structural checks, the unique roof selector, index non-sharing, output bounds, material descriptors, indices,
   and serialization complete successfully.
3. Compare the variants and confirm that only the intended roof `uv0` and tangent entries differ.
4. Build the mod through its ordinary package path and verify the packaged DLL and both models match their current
   sources; review the two generated outputs as one set.
5. Verify the exact blueprint child identities and inspect runtime logs for missing-model, missing-child, selection, or
   optimizer failures.
6. Validate normal preview behavior, deterministic primary/secondary activation, geometry, placement, and roof
   appearance in the real game.
7. Before claiming broader lifecycle completion, validate save/load restoration and the relevant linked-building
   logistics scenarios separately.

Preserve the already validated geometry while testing one evidence-supported model or lifecycle hypothesis at a time.

## DualDistrictTank Generated Model Ownership

`Tools/create_tank_half_timbermesh.py` owns the independent tracked model
`Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.Folktails.Model.timbermesh`, whose resource identity is
`DualDistrictTank.Folktails.Model`. It reuses the established Timbermesh parsing and clipping helpers but is not a member
of the warehouse model's atomic two-output set.

The generator clips the entrance-side half of the exact stock Medium Tank model at Timbermesh Z = 1. Revalidate the
source identity, bounds, one-node representation, and complete entrance-side pipe geometry when the game asset changes.
The ordinary mod build copies this loose Timbermesh into the compatibility lane; Unity export is not part of its
ownership path.

## DualDistrictTank Blueprint Contract

Each physical tank entity is the entrance-side 2x1 half of the stock 2x2x3 Medium Tank. Two opposite placements link
into one visual tank with opposite entrances and the existing shared replica inventory behavior.

The blueprint must keep a valid `#Unfinished` model using
`ConstructionBases/ConstructionBase1x2/ConstructionBase1x2.blueprint`. Its ground-level entrance participates in
doorstep spawning, so `PlaceFinished` does not make an empty `UnfinishedModelName` valid.

The tank remains liquid-only and retains the stock logical capacity and stock liquid height/movement behavior while
those gameplay decisions remain part of the prototype. Building costs, construction stages, final player-facing text,
liquid-type scope, textures, and release metadata are not final.

## DualDistrictTank Liquid Visualization Contract

The blueprint uses the stock `Liquid/2x2` plane at zero `CenterOffset`. Both physical halves retain their own
visualizer; do not replace them with a stretched `Liquid/1x1` mesh or one full plane owned by only one half.

After the owning visualizer assigns a real nonempty source mesh, the runtime component creates one instance-owned half
mesh. It clips source triangles against local Z <= 0, interpolates the verified channels, translates retained geometry
by local Z + 0.5 toward the inner seam, and preserves the stock material, amount, height, highlight, and movement
ownership.

The current source is intentionally bounded as a flat, static, single-submesh liquid plane. Do not generalize its
channel set, one-submesh output, duplicated intersection vertices, or tangent handling to arbitrary Unity meshes.

The late lifecycle guard must restore the owned half mesh when the stock visualizer reassigns its source, without
rebuilding geometry every frame. Structural failure must latch after one diagnostic, and entity deletion must destroy
the cloned native mesh.

## DualDistrictTank Validation

After changing the tank model, blueprint, inventory behavior, or liquid visualization:

1. Regenerate and structurally validate the tank-half Timbermesh when its source transformation changed.
2. Build through the ordinary package path and verify the packaged DLL and model match current sources.
3. Verify the valid unfinished model, doorstep placement, opposite entrances, and linked inventory in the real game.
4. Test the liquid surface selected and unselected so each entity's half ownership, seam, bounds, and highlight are
   visible.
5. Exercise source reassignment through initialization and relevant allowed-liquid changes; inspect a fresh runtime log
   for split, model, selection, or lifecycle failures.
6. Before claiming broader completion, validate save/load, deletion cleanup, logistics, reservations, construction,
   capacity behavior, and the remaining prototype decisions separately.
