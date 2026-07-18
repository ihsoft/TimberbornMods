# DualDistrictWarehouse Agent Instructions

These instructions apply to work under `DualDistrictWarehouse/`. Follow the repository-wide instructions and
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
