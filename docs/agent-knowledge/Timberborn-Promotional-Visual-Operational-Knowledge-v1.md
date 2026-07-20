# Timberborn Promotional Visual Operational Knowledge

## Purpose

Provide a focused workflow for creating, updating, owning, and validating external promotional images for Steam
Workshop, Mod.IO, GitHub Releases, and similar publication surfaces.

This document covers thumbnails, previews, slideshow images, promotional screenshots, and banners. It does not cover
tool, building, menu, or UI sprites loaded by the game; use Timberborn Icon Operational Knowledge for those assets.
Publishing or changing a public platform remains governed by the release rules and explicit authorization.

## Establish The Intended Change

Determine whether the task is a new composition, an approved redesign, or a bounded update to an established design.
For a bounded update, identify the invariants that must survive: typography, annotations, overlay geometry, grading,
subject relationships, and delivery dimensions. Do not treat stylistic plausibility as permission to change them.

Avoid adding or intensifying vignette, blur, color grading, shadows, or other subjective effects unless the requested
design needs them. Preserve map and gameplay readability when an approved composition is meant to remain recognizable.

## Separate Editable Inputs From Outputs

Classify each artifact by ownership:

- raw or deliberately cropped gameplay image;
- transparent annotation/typography overlay or layered editable source;
- deterministic compositor and explicit framing parameters, when generation is expected;
- reviewable high-resolution master;
- platform-sized delivery output.

A flattened master or delivery image is an output, not automatically an editable source. Recovering overlays from its
pixels can copy obsolete background fragments, damage typography, or compound earlier transformations. Do not promote a
frame-specific recovery script that reads a changing output as its own source.

If future gameplay-image substitutions are expected, prefer independently owned immutable inputs and a reproducible
composition path. If the image is intentionally one-off, tracking only the approved flattened output can be reasonable,
but record that future layer replacement is not reproducible. Do not rely on chat attachments, temporary directories,
or an accidentally retained screenshot as repository ownership.

The repository does not have to retain every large raw screenshot. It does need an explicit decision: track the source,
store an established layered format elsewhere with discoverable ownership, or accept and report the regeneration limit.

## Frame The Semantic Safe Region

Persistent titles, labels, and annotations reduce the useful gameplay region even when the final canvas keeps a common
aspect ratio. Frame against that remaining semantic safe region rather than using an ordinary full-frame cover crop.

Identify the subjects that communicate the feature, scale them for the useful window, and use empty source margins as
editing latitude. Keep important subjects clear of overlays and delivery edges. Validate annotation alignment against
the referenced subjects, not against mathematically symmetric canvas positions when the scene itself is asymmetric.

Do not preserve empty ground or margins merely because they existed in the source screenshot. Conversely, do not crop
so tightly that the gameplay relationship, orientation, or surrounding context becomes unclear.

## Preserve Approved Checkpoints

Keep the last approved candidate recoverable before subjective refinement. Prefer a tracked or reproducibly generated
checkpoint over an ignored temporary copy. A temporary rollback image can bound one editing session, but it is not a
durable source or ownership record.

## Validate The Delivery Artifact

Review both the master and every actual platform output:

1. Verify expected dimensions, aspect ratio, color mode, and compression format.
2. Compare the result with the approved composition invariants and confirm that only authorized elements changed.
3. Inspect useful-window occupancy, subject scale, annotation alignment, contrast, and edge treatment.
4. Review typography, thin lines, shadows, and labels at final delivery size, not only while zoomed into the master.
5. Inspect the compressed delivery file for blur, ringing, lost detail, or illegible annotations.

Successful generation of a correctly sized image does not establish a successful promotional composition. The review
artifact is the exact image the platform or publishing tool will consume.

## Knowledge Boundaries

Keep exact source images, text, overlay identities, coordinates, safe regions, compositor commands, master paths, and
delivery paths in the closest local instructions. Do not turn one accepted crop or effect decision into a portable
visual default.
