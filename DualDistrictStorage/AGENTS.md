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

## Derived Iron Teeth Model And Material Ownership

`Tools/copy_timbermesh_model.py` owns three faction-specific Iron Teeth models derived one at a time from the tracked
Folktails outputs. Each invocation requires `--timbermesh-plugin-dir`, `--source`, `--source-model`, `--output`, and
`--output-model`, plus one explicit `--material SOURCE=OUTPUT` argument for every source material. Do not hand-copy or
hand-edit these binary assets:

- `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.Model.timbermesh`, identity
  `DualDistrictWarehouse.Folktails.Model`, produces
  `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.IronTeeth.Model.timbermesh`, identity
  `DualDistrictWarehouse.IronTeeth.Model`;
- `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.MirroredRoof.Model.timbermesh`, identity
  `DualDistrictWarehouse.Folktails.MirroredRoof.Model`, produces
  `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.IronTeeth.MirroredRoof.Model.timbermesh`, identity
  `DualDistrictWarehouse.IronTeeth.MirroredRoof.Model`;
- `Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.Folktails.Model.timbermesh`, identity
  `DualDistrictTank.Folktails.Model`, produces
  `Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.IronTeeth.Model.timbermesh`, identity
  `DualDistrictTank.IronTeeth.Model`.

Both warehouse variants use this complete material map:

- `BaseWood_LightBrown.Folktails` -> `BaseWood_Grey.IronTeeth`;
- `Paper.Folktails` -> `Paper.IronTeeth`;
- `BaseWood_Brown.Folktails` -> `BaseWood_DarkBrown.IronTeeth`;
- `IrregularPlanks_Brown.Folktails` -> `IrregularPlanks_DarkBrown.IronTeeth`;
- `BaseWood_White.Folktails` -> `BaseWood_Indigo.IronTeeth`;
- `IrregularPlanks_Mossy.Folktails` -> `IrregularPlanks_Indigo.IronTeeth`.

The tank variant uses this complete material map:

- `BaseWood_LightBrown.Folktails` -> `BaseWood_Grey.IronTeeth`;
- `Paper.Folktails` -> `Paper.IronTeeth`;
- `BaseWood_Brown.Folktails` -> `BaseWood_DarkBrown.IronTeeth`;
- `WindowsAtlas.Folktails` -> `WindowsAtlas.IronTeeth`.

The tool must parse the source through the official Timbermesh protobuf schema, require the expected one-node source
identity, enumerate the complete source material set, reject missing mappings and mappings for unknown materials,
change the model and material identities, serialize and parse the result again, and verify the exact output identity
and material set. While these resources remain derived copies, verify that geometry, indices, vertex properties, and
other non-material model payload match their Folktails sources. Material descriptors are intentionally different.
Regenerate a derived output whenever its source or mapping changes, then verify reproducible output, source/package
hashes, the ordinary package build, and target-faction material resolution in the game log.

Iron Teeth blueprints must reference only the corresponding Iron Teeth model identities. The derived files are not
members of the warehouse generator's atomic Folktails output set; their separate copy-tool ownership is intentional.
If faction-specific art later diverges, transfer each affected output to an appropriate reproducible generator and
update its blueprint contract in the same change rather than editing the binary manually.

The Iron Teeth template collection keeps `TemplateCollectionSpec.CollectionId` equal to `Buildings.IronTeeth` and
lists both building resources through `Blueprints`. Do not reintroduce the obsolete `TemplatePaths` shape.

The transformation, build, packaged-file hashes, target-faction material resolution, toolbar availability, and rendered
Iron Teeth finished models have been confirmed in the real game.

## Current Model Contract

The Folktails warehouse uses two generated variants of the same entrance-side 3x1 geometry under one `#Finished` root:

- `#Finished/NormalModel` references
  `Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.Model`;
- `#Finished/MirroredRoofModel` references
  `Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.MirroredRoof.Model`.

The single `CollidersSpec` remains on `#Finished`; do not add separate colliders to the model children without evidence
that the building ownership model changed.

Every instance defaults to `NormalModel` during `Awake`. After linking, the existing `PrimaryHalf()` GUID ordering keeps
the primary half normal and activates `MirroredRoofModel` only on the secondary half. Both peers must compute the same
role, and a missing expected child identity must remain a diagnostic warning rather than a silent fallback.

`BuildingLinked` may run before the peer's reciprocal link field has been assigned. Determine the secondary directly
from the two callback participants, equivalent to `ReferenceEquals(primary, this) ? _linked : this`; do not dereference
`primary._linked` or otherwise assume reciprocal callback completion during unfinished placement.

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

## Construction Model Ownership

`Tools/create_construction_half_timbermesh.py` owns five independently generated construction models. It accepts the
current game `resources.assets`, UnityPy and official Timbermesh schema directories, exact source and output identities,
and an output path. It requires one source node, clips the entrance-side half at Timbermesh Z = 1, validates the result,
and round-trips the compressed model. Do not hand-edit these outputs:

- `ConstructionBase2x2.Model` produces
  `Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.ConstructionBase.Model.timbermesh`, identity
  `DualDistrictTank.ConstructionBase.Model`;
- `MediumTank.Folktails.ConstructionStage0.Model` produces
  `Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.Folktails.ConstructionStage0.Model.timbermesh`, identity
  `DualDistrictTank.Folktails.ConstructionStage0.Model`;
- `MediumTank.IronTeeth.ConstructionStage0.Model` produces
  `Mod/Buildings/Storage/DualDistrictTank/DualDistrictTank.IronTeeth.ConstructionStage0.Model.timbermesh`, identity
  `DualDistrictTank.IronTeeth.ConstructionStage0.Model`;
- `MediumWarehouse.Folktails.ConstructionStage0.Model` produces
  `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.ConstructionStage0.Model.timbermesh`,
  identity `DualDistrictWarehouse.Folktails.ConstructionStage0.Model`;
- `MediumWarehouse.IronTeeth.ConstructionStage0.Model` produces
  `Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.IronTeeth.ConstructionStage0.Model.timbermesh`,
  identity `DualDistrictWarehouse.IronTeeth.ConstructionStage0.Model`.

The tank construction base is intentionally common because both stock faction tank blueprints reference the same
`ConstructionBase2x2.Model`. Tank and warehouse stages remain faction-specific and retain their source faction's
material identities. Revalidate those ownership facts before changing the source game version or introducing new
faction art.

## Construction Blueprint Contract

The current unfinished models use direct child 0 as the construction base and direct child 1 as the generated
`ConstructionStage0`. Keep `ConstructionSiteProgressVisualizerSpec.ProgressThresholds` equal to `[0.0]` for that
two-state structure. Do not add, remove, or reorder direct children without updating and validating the threshold list.

Each faction blueprint must reference its faction-specific generated stage. Both tank factions must reference the common
`DualDistrictTank.ConstructionBase.Model`; do not compose the initial tank state from two independent
`ConstructionBase1x2` models because their perimeter geometry creates a visible seam.

The warehouse and tank use their own stock-derived construction sources. Do not substitute a dimensionally compatible
stage from another building. The tank keeps `PlaceFinished` disabled and uses the stock Medium Tank construction costs
so ordinary construction exercises the intended lifecycle.

## Construction Model Validation

After changing a construction generator, source, output, blueprint child, threshold, cost, or linking callback:

1. Regenerate each affected output through the owning script and verify source identity, one-node structure, Z = 1
   clipping, output identity, bounds, indices, materials, and serialization.
2. Build through the ordinary package path and verify every affected source/package hash.
3. Verify faction TemplateCollection registration, blueprint model identities, child order, and the exact threshold.
4. Place and construct both factions normally in the real game; `PlaceFinished` or instant build is not sufficient.
5. Inspect the initial base, every stage, the completed model, seams, linking behavior, and a fresh log. Confirm that
   exactly one intended unfinished state is visible at a time.

Ordinary construction has confirmed the warehouse stages, the common composed tank base, the faction tank stages,
stage switching, linked placement, and target-faction model loading. Broader logistics and save/load behavior remain
outside that evidence.

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

The blueprint must keep the valid `#Unfinished` base, faction stage, and progress visualizer defined by the Construction
Blueprint Contract. Its ground-level entrance participates in doorstep spawning, so an empty `UnfinishedModelName` is
not valid. Both faction blueprints keep `PlaceFinished` disabled and use the stock Medium Tank construction costs.

The tank remains liquid-only and retains the stock logical capacity and stock liquid height/movement behavior while
those gameplay decisions remain part of the prototype. Broader liquid-type scope, final textures, and release metadata
remain separate decisions.

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
3. Verify ordinary construction, seam-free common-base composition, faction stage, progress switching, doorstep
   placement, opposite entrances, and linked inventory in the real game.
4. Test the liquid surface selected and unselected so each entity's half ownership, seam, bounds, and highlight are
   visible.
5. Exercise source reassignment through initialization and relevant allowed-liquid changes; inspect a fresh runtime log
   for split, model, selection, or lifecycle failures.
6. Before claiming broader completion, validate save/load, deletion cleanup, logistics, reservations, construction,
   capacity behavior, and the remaining prototype decisions separately.
