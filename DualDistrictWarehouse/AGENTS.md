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

The building geometry and opposite-entrance placement have been confirmed in the real game. Roof texture appearance is
a known unresolved issue, so the current result is a working intermediate model rather than a finished textured asset.
Preserved UVs are evidence of a conservative transformation, not evidence that the roof atlas is correct.

## Model Change Validation

After changing the generator or generated model:

1. Regenerate the tracked Timbermesh through the owning script.
2. Verify structural checks, output bounds, indices, material names, and serialization complete successfully.
3. Build the mod through its ordinary package path and verify the packaged model matches the generated source.
4. Validate geometry and placement in the real game.
5. Validate roof textures, UV orientation, materials, and shading separately before describing the model as finished.

Do not speculate that a UV, material, polygon-order, or vertex-color change fixes the roof. Preserve the already
validated geometry while testing one evidence-supported appearance hypothesis at a time.
