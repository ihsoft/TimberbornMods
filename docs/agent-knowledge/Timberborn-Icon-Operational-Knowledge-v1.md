# Timberborn Icon Operational Knowledge

## Purpose

Provide a focused workflow for creating, modifying, importing, packaging, and validating player-facing Timberborn game
icons distributed as loose PNG or JPG assets.

This document does not define the visual identity of a particular mod. Exact source identities, generated output paths,
overlays, coordinates, and accepted compositions belong in the closest local instructions.

## Start From The Intended Visual Relationship

Determine whether the requested icon is new artwork or a deliberate derivative of an existing game or mod icon before
choosing a tool.

For a derivative icon, locate and inspect the exact source asset first. Record its resource identity, native dimensions,
color format, transparency, and relevant alpha bounds. Preserve the source resolution and unchanged pixels unless the
design explicitly requires a redraw or rescale. A visually similar recreation is not equivalent to the source and can
introduce avoidable differences in silhouette, texture, sharpness, or alignment.

AI-generated imagery can be useful for genuinely new artwork or exploration, but it is not evidence that a derivative
matches Timberborn's current visual language. Prefer deterministic composition from verified source assets when the
requested distinction is an overlay, badge, crop, recolor, or other bounded transformation.

## Keep Generated Derivatives Reproducible

When a script generates a tracked icon, document the script as the owner and give it explicit source-to-output mapping.
Treat the generated image and its import metadata as owned outputs rather than hand-maintained artwork.

Make the transformation narrow and verifiable. Where applicable, check that pixels outside the intended edit region
remain identical to the source. Do not assume one overlay size or position fits a different source silhouette; make the
relationship explicit and re-evaluate it for every new icon family.

## Import And Package Loose Icons Deliberately

Timberborn can load raw PNG and JPG assets from a mod package. Keep the adjacent image metadata file with the image when
the loader uses it to define import behavior; an image that looks correct on disk may otherwise be imported with
unsuitable defaults.

For a UI sprite icon, verify the current repository and loader conventions for at least:

- sprite import rather than a generic texture;
- native width and height;
- mipmap generation;
- filtering and wrap mode;
- color/texture format and alpha preservation;
- anisotropic filtering when represented by the metadata schema.

No single setting is correct for every texture use. For small menu icons, explicit no-mipmap sprite metadata and edge
handling commonly prevent blur or bleeding, but confirm the intended use and current loader behavior instead of copying
settings mechanically.

## Validate At The Player's Scale

Validate both the artifact and its actual presentation:

1. Verify source identity, output dimensions, color/alpha format, and the bounded pixel transformation.
2. Parse or otherwise validate the adjacent metadata and confirm the blueprint or UI resource references the intended
   icon identity.
3. Build through the owning package path and verify the packaged image and metadata match their tracked sources.
4. Inspect the icon in the real game at its normal menu or control size and against the relevant background and state.
5. Check that the base subject remains recognizable and that the added distinction neither disappears nor dominates it.

Native-resolution inspection alone cannot establish readability. Details that look clear at source resolution may
vanish at menu scale, while a large badge may obscure the building or action the icon is meant to communicate.

## Knowledge Boundaries

Do not generalize a mod's accepted coordinates, badge count, overlay size, source icon, or generated output contract.
Keep those facts local. Route Unity-owned textures and asset-bundle workflows through Timberborn Unity Operational
Knowledge; route UI hierarchy and interaction work through the UI Toolkit notes.
