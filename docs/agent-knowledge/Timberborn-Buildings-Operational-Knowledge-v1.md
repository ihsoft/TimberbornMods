# Timberborn Buildings Operational Knowledge

## Purpose

Provide a focused workflow for creating, modifying, packaging, and validating Timberborn building blueprints,
registration, block layouts, entrances, construction states, placement behavior, and selection colliders.

This document does not replace C# mod-integration rules, Timbermesh or Unity asset workflows, repository package
validation, or a mod's local building contracts. Load those routes as well when the task crosses their boundaries.

## Loose-File Blueprint Packages

For loose `.blueprint.json` files owned directly by a mod package, treat the compatibility-lane directory as the path
root. A blueprint referenced as `Buildings/...` or `TemplateCollections/...` must be packaged at that path relative to
the lane. Do not insert a `Blueprints/` prefix merely because extracted game assets use that organizational directory;
use such a prefix only when the owning export pipeline explicitly requires and produces it.

Before real-game validation, inspect every changed `BlockObjectSpec` and verify that `Blocks.Count` equals
`Size.X * Size.Y * Size.Z`. Use the closest stock blueprint to establish the meaning and order of block entries rather
than padding an incomplete list mechanically. A missing entry can fail during eager preview creation before the mod's
intended behavior can be tested.

When nesting another blueprint, copy the exact current stock child structure. In the confirmed current shape,
`BlueprintPath` belongs to a named entry ending in `#nested` under `Children`; do not place it directly on the owning
entity node, where the loader can interpret it as a component-spec key rather than a nested-blueprint reference.

For faction building registration, inspect a current stock `TemplateCollectionSpec` instead of copying an older
collection shape. In the confirmed current schema, the spec uses `CollectionId` for the target collection and
`Blueprints` for its resource paths; an obsolete `TemplatePaths` shape can parse as authored data yet leave the
buildings absent from the faction toolbar. Verify the exact faction collection identity and selectable tools in the real
game.

## Hidden And Programmatic Building Templates

Do not infer that a building template is invisible to global systems merely because it is absent from a player-facing
`TemplateCollection`. A template carrying `BuildingSpec` can participate in consumers that enumerate all building
specifications, including systems unrelated to its intended programmatic placement.

For every hidden or programmatically placed `BuildingSpec`, inspect current global consumers and the companion specs
they read after accepting the template. Provide complete companion data such as `PlaceableBlockObjectSpec` when those
consumers require it, then select availability, tool-group, achievement, and dev-mode exclusions from current evidence.
Do not omit a companion spec as a visibility mechanism or copy exclusion flags without verifying their effects.

## Ground-Level Entrances And Unfinished Models

In the confirmed current game lifecycle, a ground-level building entrance without `DoorstepSpawnDisablerSpec` requires
a valid unfinished-model transform. Doorstep spawning parents its object to `BuildingModel.UnfinishedModel.transform`
without treating a missing model as valid.

`PlaceFinished` controls placement state; it does not remove this lifecycle dependency. Even a temporary
`PlaceFinished` prototype must provide a valid `UnfinishedModelName` and referenced model when doorstep spawning remains
enabled. Disable doorstep spawning only when the building intentionally does not need it and current architecture
evidence supports that decision.

## Construction States And Selection

When `ConstructionSiteProgressVisualizerSpec` controls an unfinished model with N direct children, child 0 is the
construction base and each following child is a progress stage. `ProgressThresholds.Count` must equal the direct child
count minus one, in the same stage order. For a base plus one stage, the stock-shaped threshold list contains one value.
Derive the actual threshold values from the closest current stock building instead of inventing them.

Do not add progress-stage children without the owning visualizer contract. Without stage switching, multiple direct
children can remain active and overlap even when every referenced model is individually valid.

Audit collider and selectability ownership for every mutually exclusive visible state. A collider below a later inactive
stage cannot make the currently active construction base selectable. Validate normal-view selection at zero progress,
at every stage, and after completion; construction-mode selection through occupied blocks can mask a missing active
collider.

When changing construction models, costs, stage order, entrances, colliders, or linking behavior, validate ordinary
unfinished placement and progression in the real game. `PlaceFinished` or instant-build/dev-mode placement does not
exercise the unfinished lifecycle, stage switching, callback order, or every selection path and cannot satisfy this gate
by itself. Verify the initial base, every stage transition, the final model, relevant logs, and that exactly one intended
construction state is visible and selectable at a time.

## Custom Placement And Preview Ownership

Treat placement and preview as separate ownership surfaces when a tool-only preview blueprint does not instantiate the
same physical templates that a custom placer creates. Correct real placement does not establish that the preview owns
the full footprint, entrances, markers, or other player affordances.

Inspect which preview components receive the tool blueprint, derive any missing affordances from the positioned preview
and current orientation, and validate footprint and entrance communication under every supported rotation. Do not
expect markers owned only by hidden placed entities to appear in a structurally different preview.

## Knowledge Boundaries

Keep exact building resource identities, source models, split coordinates, placement offsets, visualization shares,
generated outputs, and owning scripts in the closest local instructions. Route custom C# placers, decorators, DI, and
Harmony integration through the general Timberborn modding rules as well as this document.
