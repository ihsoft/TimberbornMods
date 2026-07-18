# Timberborn Runtime Unity Mesh Operational Knowledge

## Purpose

Provide an evidence-based workflow for per-instance runtime changes to `UnityEngine.Mesh` geometry, UVs, tangents, and
visualization surfaces in Timberborn.

This document begins after asset import. It does not define Timbermesh serialization, general 3D modeling, or a
universal clipping library. Bound every transformation to the channels, topology, materials, lifecycle, and ownership
actually verified for its input mesh.

## Find The Runtime Mesh Owner

Before changing a runtime mesh, establish:

- which system creates or imports the source mesh;
- which component assigns it to the renderer or `MeshFilter`;
- whether the source is empty until initialization;
- every lifecycle path that can replace or clear it;
- whether other components run before or after the proposed hook;
- whether the mesh is shared across templates, entities, or renderers.

Do not assume listener registration or finished-state callback ordering. A mutation that runs before the owning
visualizer initializes may see an empty mesh and can be overwritten by a later assignment.

Prefer an explicit post-initialization event or ordered hook when the current API provides one. When it does not, a
bounded late callback may wait for a nonempty source and use a cheap reference comparison to detect later reassignment.
Build transformed geometry only when the source reference changes; do not rebuild it every frame.

## Own Per-Instance Meshes Explicitly

Never mutate a stock or cached shared mesh when only one entity or visualization should change.

For an instance-owned transformation:

1. Wait for the real source mesh.
2. Clone or construct the transformed mesh.
3. Assign the owned mesh only to the intended instance.
4. Retain enough state to recognize source reassignment and restore the transformed mesh idempotently.
5. Destroy the owned native mesh when its entity or visualization is deleted.

A managed reference becoming unreachable does not by itself establish timely native Unity object cleanup. Give
per-instance cloned meshes an explicit lifetime owner.

## Clip Geometry Instead Of Dropping Crossing Triangles

Deleting every triangle that crosses a cut plane creates holes and can remove a large fraction of small or low-poly
surfaces. Clip each intersecting triangle against the retained half-space and triangulate the retained polygon.

For every generated intersection vertex, preserve and interpolate every channel present and required by the verified
input, such as:

- position;
- normal;
- tangent and handedness;
- UV sets;
- vertex color.

Renormalize directional channels and validate shader-specific tangent behavior. Preserve submesh boundaries and
material ownership unless the input is explicitly validated as a bounded single-submesh case. For reusable or larger
geometry, share intersection vertices along the same source edge instead of duplicating them per output triangle.

Do not generalize a flat static triangle mesh implementation to meshes with unsupported topology, multiple material
regions, additional UV streams, bone weights, bind poses, blend shapes, skinning, vertex animation, or other data that
the transformation does not preserve.

## Establish Coordinate And Visualization Ownership

Separate these concerns before choosing a fix:

- mesh shape and local geometry;
- object transform and scale;
- building or grid position;
- visualization height and nonlinear movement;
- material, highlight, and amount updates.

Scaling a circular source mesh creates an ellipse; it is not equivalent to clipping or composing circular halves.
Likewise, a fixed offset is not automatically relative to a building entrance or orientation.

In the confirmed current `StockpilePlaneVisualizer` call chain, horizontal `CenterOffset` is added in grid/world axes,
while building orientation is applied to mesh rotation separately. Do not use a fixed nonzero horizontal
`CenterOffset` as an entrance-relative seam offset without revalidating the current coordinate calculation.

Keep stock height, amount, material, and nonlinearity behavior when only the visualization footprint needs to change.
Modify the narrowest owner of the incorrect shape.

## Make Recurring Diagnostics One-Shot

A failure path inside `LateUpdate`, another recurring callback, or an idempotent reapplication guard must latch or
disable itself after reporting a stable failure. Do not emit the same structural error every frame.

Successful compilation, DLL replacement, or package hash equality proves deployment, not that the runtime mutation
found its source mesh or executed. Keep a clear diagnostic for missing, empty, replaced, or structurally unsupported
meshes, then verify runtime logs.

## Validate Ownership And Appearance

Validate more than the final unselected screenshot:

- inspect the visualization selected and unselected;
- use highlighting when it reveals which entity owns which geometry;
- verify bounds, seams, overhangs, material, height, and shading;
- exercise every known source-reassignment path, including initialization, load restoration, and allowed-good changes;
- verify repeated reapplication does not allocate another mesh;
- verify deletion destroys the instance-owned mesh;
- inspect a fresh runtime log after the successful run.

Selection and highlighting are useful evidence because they can expose per-entity ownership and out-of-bounds geometry
that is difficult to see from the normal material alone.

## Stop Conditions

Stop and investigate instead of broadening the implementation when:

- the source-mesh owner or assignment order is unknown;
- the source is empty or repeatedly replaced for unexplained reasons;
- the proposed change would mutate a shared stock or cached mesh;
- crossing triangles are discarded where a continuous surface is required;
- present vertex channels or submesh/material ownership cannot be preserved;
- the input is skinned, animated, non-triangular, or otherwise outside the validated representation;
- coordinate-frame ownership is not established;
- a recurring hook rebuilds geometry or repeats diagnostics every frame;
- native mesh lifetime has no explicit cleanup owner;
- save/load or another required reassignment lifecycle remains unverified.
