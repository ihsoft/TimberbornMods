# Timberborn Timbermesh Operational Knowledge

## Purpose

Provide an evidence-based workflow for locating, extracting, modifying, validating, and packaging Timberborn
`.timbermesh` models.

This knowledge covers the lifecycle of extracted, derived, or generated Timbermesh assets. Geometry clipping is one
supported transformation, not the document's defining purpose. It does not define general 3D modeling or arbitrary
Unity `Mesh` workflows.

This document is not a stable Timbermesh format specification. Binary container details discovered in game archives
are version-sensitive implementation evidence and must be revalidated against the current game and official Timbermesh
sources.

## Start From Exact Resource Identity

Before extracting or modifying a stock model:

1. Find the owning blueprint and its exact `TimbermeshSpec` resource key.
2. Confirm the expected building identity, faction, footprint, and model variant.
3. Recursively inventory available local resources by both exact name and file type.
4. Distinguish a blueprint reference from the referenced model payload.
5. Match example models by resource identity and footprint, not by a similar display name.
6. If no editable model is present, search the current game resource archives for the exact resource key.
7. Inspect the runtime asset representation before choosing an extraction tool.

Do not claim that a model is absent after checking only obvious folders or top-level search results.

## Choose The Narrowest Suitable Tool

Use the tool that matches the asset's actual representation.

A Timbermesh stored as serialized data on a Unity object may not appear as a conventional Unity `Mesh` hierarchy.
Asset extractors, Unity archive readers, Blender importers, and custom protobuf tooling solve different problems; none
is the universal default.

Prefer:

- exact resource and object names over hard-coded Unity path IDs;
- current official Timbermesh schemas and exporters where available;
- parsers that validate the expected representation and fail when its shape changes;
- the smallest extraction path that preserves the original model data.

Treat archive offsets, serialization headers, compression wrappers, and component layouts as version-sensitive.
Detection should validate the observed structure instead of assuming that offsets from a previous game build remain
correct.

Keep game archives, bulk extracted content, generated references, and external dependencies local and ignored. Do not
edit those inputs or commit them merely to make a transformation self-contained.

## Establish Coordinate Systems From Evidence

Do not infer Timbermesh axes from words such as width, height, and depth alone.

Before cutting, rotating, mirroring, or repositioning geometry, compare:

- parsed model bounds;
- the owning blueprint footprint;
- current official importer or exporter coordinate conversion;
- known placement transforms;
- a preview or real-game result when needed.

Blueprint block coordinates and Timbermesh coordinates may use different axis meanings. Record the evidence supporting
the chosen axis and plane, and revalidate it for a different model or game version.

## Prefer Existing Placement Transforms

When the game already rotates or positions repeated building parts, determine whether one normally oriented model can
serve every placement before generating mirrored variants.

For linked building halves, a normal placement rotation may produce the required opposite orientation while avoiding
negative-scale winding, normal, and tangent complications. This is a conditional design preference, not a universal
prohibition on negative scale. Verify the actual owning placement system.

## Preserve Mesh Semantics During Geometry Editing

When a clipping plane intersects triangles, deleting every crossing triangle can create holes and damage structural
details. Clip intersecting polygons when the retained surface must remain continuous.

For every generated intersection vertex:

- preserve and interpolate every vertex property present in the source;
- renormalize directional values such as normal vectors and tangent direction components;
- preserve tangent handedness according to the source representation;
- share intersection vertices across the same source edge;
- discard degenerate output triangles;
- preserve material assignments unless the task intentionally changes them.

Do not assume that position, normal, and one UV channel are the complete vertex format. Inspect the source property
layout first.

Before changing vertices selected through one source submesh or material, verify whether any of their indices are shared
with other submeshes. A per-index UV, tangent, color, or position change also affects every surface using that vertex;
split or duplicate the vertex deliberately when shared ownership is required instead of silently modifying both.

After modification, validate:

- property scalar types and byte lengths;
- vertex counts and triangle-list indices;
- index bounds;
- material slots and names;
- output bounds and the intended clipping plane;
- serialization and decompression round trips.

## Separate Geometry From Texture Correctness

A model can have correct geometry while still having incorrect atlas orientation, UV mapping, vertex colors, tangents,
or shader-driven appearance.

Treat these as separate validation gates:

1. Geometry, footprint, orientation, entrances, seams, and placement.
2. Materials, UVs, texture direction, atlas appearance, normals, tangents, and shading.

Preserving existing UV channels is a conservative starting point, not proof that duplicated, clipped, or rotated
surfaces will remain visually correct. Reusing stock material names may preserve material resolution, but material names
alone do not reproduce or validate the stock texture layout.

Validate textured appearance in the real game. Do not describe an asset as finished based only on geometry preview or
successful loading.

## Choose Asset Variants Or Runtime Mutation Deliberately

When a small, deterministic visual difference is known before import, evaluate generating explicit Timbermesh variants
instead of mutating an optimized runtime mesh. An asset-side transformation can use confirmed source submesh identity,
UV layout, materials, and tangents before prefab optimization removes or merges that structure.

A single template may contain several variant model children and activate exactly one according to a deterministic
runtime role. This can preserve one construction or placement workflow without requiring separate templates, but only
when the owning lifecycle reliably reapplies the selection after creation, linking, and save/load restoration.

For deterministic variant activation, establish:

- a safe default before previews or new instances become visible;
- a total ordering or other role rule that every peer computes identically;
- whether the role inputs persist across save/load and entity recreation;
- the lifecycle event that reapplies selection after a relationship is restored;
- whether either physical role assignment is valid when newly created identities are arbitrary.

Also inspect non-rendering template contracts. Variant children can interact with bounds, colliders, selection,
visibility walkers, animations, LOD, previews, or external systems that enumerate inactive descendants even when only
one renderer is visible.

Generated variants trade runtime selection complexity for other costs:

- larger package output and more generated-file contracts;
- possible import, optimization, and memory cost even for inactive children;
- stable child-name and activation contracts;
- variant proliferation when several dimensions can differ;
- regeneration work whenever the source asset or transformation changes.

Do not assume an inactive child is free. In confirmed current Timberborn pipelines, model import and several prefab
optimizer stages traverse inactive descendants, so every variant can contribute import, optimization, and cached-prefab
cost before an entity instance becomes active. Revalidate the chain for the target game version instead of treating
this observation as a permanent engine contract.

Prefer evaluating asset-side variants when the variant count is small, models are reasonably sized, the distinction is
static, source selectors are verified, and generation is reproducible. Prefer runtime mutation when the transformation
depends on dynamic state, variant combinations would multiply, duplicated assets are too costly, or no stable
pre-import representation is available.

Do not assume either approach is cheaper without evidence. Compressed package bytes do not measure native mesh or
material memory. When the cost could affect the decision, compare mesh and material identities across variants and
instances, measure their runtime memory through current engine diagnostics, and distinguish one-time cached-prefab
cost from per-instance duplication. Validate package contents, load and optimization behavior, deterministic
activation, save/load restoration, logs, and real-game appearance.

## Account For Runtime Prefab Optimization

Before editing a Timbermesh after instantiation, inspect the current prefab import and optimization chain. Source mesh
structure, submesh material names, and source-space UV ranges may not survive import, auto-atlasing, or material-based
mesh merging.

Do not select a runtime surface solely by its source material name unless current runtime evidence confirms that the
identity survives. Use a property that remains observable after optimization, or move the transformation to an earlier
stage where the required identity is still available.

Auto-atlased UVs may occupy only a fragment of the runtime atlas. A transform expressed against the full 0..1 range can
move coordinates outside that fragment. Establish the runtime UV interval and verify the source UV relationship and
atlas transform before applying a fragment-relative transform.

When the source UV relationship is known to be symmetric and the current atlas mapping is affine, mirror U within the
observed runtime fragment as `U' = Umin + Umax - U`. This preserves the occupied atlas interval, unlike a global
`1 - U`. The formula is conditional on those verified properties; it is not a universal UV repair.

Before changing a mesh for only one instantiated object or linked part, clone the shared mesh so the mutation does not
affect other users of the same asset. When reflecting UV orientation, update tangent direction and handedness as
required by the affected shader data instead of changing UV values alone.

A rebuilt DLL or correctly replaced package proves deployment, not that the runtime mesh selection or mutation ran.
Provide a diagnostic failure path, inspect runtime logs, and verify the intended appearance in the real game.

## Determine Packaging Ownership

Determine whether the model is owned by Unity export or by a loose-file package workflow before selecting the build
path.

For a loose `.timbermesh` supported by the current game and official documentation:

- keep the source or generator under the owning mod;
- place the generated model in the package path expected by its resource key;
- verify whether `TimbermeshSpec` references the resource without the `.timbermesh` suffix;
- use the ordinary mod build when it already copies the asset into the compatibility lane;
- do not introduce a Unity export merely because the asset is a 3D model.

After packaging, compare the source and packaged asset hashes and inspect the real package output. Do not generalize
this workflow to Unity-owned bundles or other model formats.

## Keep Generated Models Reproducible

When a generated Timbermesh is tracked as mod content, keep the transformation reproducible.

The generator should:

- accept explicit source archive, dependency, resource identity, output identity, and output path inputs;
- locate assets by stable identity rather than current archive object numbers;
- reject missing or ambiguous payloads;
- validate input and output structure;
- produce deterministic model data where practical;
- report useful geometry counts and bounds;
- fail rather than silently adapting to an unknown serialization shape.

The tracked generated asset and its generator have different ownership from ignored game archives and local parser
dependencies. Never commit the latter merely to make regeneration self-contained.

When one generator invocation produces several coordinated models that one blueprint or runtime contract consumes
together, treat those outputs as one atomic generated set. Regenerate, validate, package, and compare the complete set;
do not update only one member. Change generator arguments, resource identities, consumer references, and activation
contracts together when their shared interface changes.

Do not impose this atomicity on outputs that are intentionally generated and consumed independently. Derive it from the
generator interface and consumer ownership contract, not merely from files sharing a directory or source model.

## Stop Conditions

Stop and investigate instead of guessing when:

- the blueprint resource identity or model variant is ambiguous;
- a matching name refers only to a blueprint or different-footprint example;
- the runtime representation differs from the parser's validated shape;
- more than one plausible model payload is found;
- axes or clipping planes cannot be established from bounds and blueprint evidence;
- required vertex properties cannot be preserved;
- target vertex indices are shared with surfaces that must remain unchanged;
- output structural validation fails;
- variant role selection cannot be made deterministic across the required lifecycle and persistence boundaries;
- geometry works but texture or shading appearance remains unexplained;
- derived stock asset ownership or distribution expectations are unclear.
