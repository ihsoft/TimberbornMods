# DualDistrictWarehouse Agent Instructions

These instructions apply to work under `DualDistrictWarehouse/`. Follow the repository-wide instructions and
Timberborn Timbermesh Operational Knowledge as well.

## Generated Model Ownership

`Mod/Buildings/Storage/DualDistrictWarehouse/DualDistrictWarehouse.Folktails.Model.timbermesh` is tracked generated mod
content. `Tools/create_half_timbermesh.py` owns its reproducible transformation; do not hand-edit the binary model.

The generator reads the exact stock `MediumWarehouse.Folktails.Model` object, renames the output root to
`DualDistrictWarehouse.Folktails.Model`, and clips the entrance-side half at Timbermesh Z = 1. Its archive, UnityPy, and
official Timbermesh protobuf inputs are local external dependencies and must remain ignored.

The script is intentionally specialized and must fail if the source representation no longer matches its validated
one-node, single-payload expectations. Revalidate the current game archive and official Timbermesh schema before
adapting it to a changed game representation.

## Current Model Contract

The current building uses one entrance-side 3x1 model. Linked placement rotates the same model for the opposite half;
do not introduce a mirrored asset or negative-scale transform unless the task intentionally changes this design and the
new approach is validated.

The generated model remains a loose Timbermesh package asset. The ordinary mod build copies it into the compatibility
lane; Unity export is not part of this asset's current ownership path.

The building geometry, opposite-entrance placement, and seamless roof texture have been confirmed in the real game.

The current runtime roof correction operates on the optimized finished model and applies only to the secondary linked
half. It selects the known top-roof surface using model-specific position and normal evidence. The exact selector
bounds live with the implementation and must not be generalized to another model.

Do not restore the failed source-material-name lookup unless current runtime evidence proves that the prefab optimizer
pipeline changed. Follow Timberborn Timbermesh Operational Knowledge for the reusable atlas-fragment, mesh-cloning,
tangent, diagnostics, and real-game validation requirements.

## Model And Runtime Appearance Validation

After changing the generator, generated model, or runtime appearance correction:

1. Regenerate the tracked Timbermesh through the owning script when its source transformation changed.
2. For generated-model changes, verify structural checks, output bounds, indices, material names, and serialization
   complete successfully.
3. Build the mod through its ordinary package path and verify the packaged model and DLL match their current sources.
4. Inspect runtime logs for model-selection, optimizer, or mutation failures; successful package replacement alone is
   not sufficient.
5. Validate geometry and placement in the real game.
6. Validate roof textures, UV orientation, materials, tangents, and shading separately.

Preserve the already validated geometry while testing one evidence-supported appearance hypothesis at a time.
