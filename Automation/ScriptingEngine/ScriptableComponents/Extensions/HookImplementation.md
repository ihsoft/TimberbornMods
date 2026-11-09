# Automation Hook Implementation Notes

## Audience And Contract Boundary

This document is for maintainers changing or diagnosing the Automation hooks engine. External configuration authors
need only [HookConfiguration.md](HookConfiguration.md), which owns the full JSON and observable integration contract.
These implementation notes describe the current checkout, not a separate public API or published-release guarantee.
Read them only for implementation work; an external author need not inspect these classes or follow repository rules.

Keep implementation changes consistent with the external contract. When behavior intentionally changes, update its
author-facing description as well as these notes; do not hide a new restriction only in internal documentation.

## Implementation Map

| Responsibility | Source |
| --- | --- |
| JSON properties/defaults | [HookConfiguration.cs](HookConfiguration.cs) |
| File discovery, early validation, registration, options | [ConfiguredIntegrations.cs](ConfiguredIntegrations.cs) |
| Assembly/type identity, DI and building ownership | [ConfiguredTarget.cs](ConfiguredTarget.cs) |
| Compiled getters/actions and setter lookup | [HookMemberAccess.cs](HookMemberAccess.cs) |
| Shared postfix patches, notification dispatch, removal | [PropertySetterHooks.cs](PropertySetterHooks.cs) |
| Native editor metadata, types, numbers and runtime validation | [HookValueFactory.cs](HookValueFactory.cs), [HookValueConverter.cs](HookValueConverter.cs) |
| End-of-frame queue and scripting adapter | [ExternalScriptableComponent.cs](ExternalScriptableComponent.cs) |
| World/building saved baselines | [AbstractGlobalStatusTracker.cs](../Components/AbstractGlobalStatusTracker.cs), [AbstractStatusTracker.cs](../Components/AbstractStatusTracker.cs) |

## Initialization And Registration

Assembly selection uses the exact loaded simple name, then `Assembly.GetType` resolves the exact type name. Duplicate
loaded simple names are rejected. No fallback type enumeration or DLL loading is performed. Reflection and expression
compilation happen during world initialization; later reads/actions use compiled accessors.

IDs, result types, bounds, fixed options and registry access paths are validated before installing signal patches.
Harmony setter patches are installed before singleton `Load`, then activated after registration. Actual singleton
resolution uses the Game DI container after singleton `Load` calls. Automation does not add provider bindings or
attach provider building components.

Actual registry contents and registration conflicts are checked after loading. Dynamic options are captured in
`PostLoad`, preserving registry order. Registration finishes in `PostLoad` before Automation binds saved rules.
Providers therefore need state and registries ready after singleton `Load`; arbitrary delays or late registration
are not substitutes for that lifecycle boundary.

## Setter And Deferred Dispatch Boundary

The setter postfix only looks up the installed hook and adds its stable callback/source-instance pair to a `HashSet`.
Source identity is reference identity, not provider-defined value equality. Hooks whose bindings were removed are
skipped. The postfix does not traverse signals, inspect their subscribers, call provider getters, compare values,
or execute rules. Even same-value setter calls can schedule work.

Once Automation is ready, the `WaitForEndOfFrame` handler performs these steps in the same processing pass:

1. Dispatch queued hooks to their signal bindings.
2. Filter source ownership and subscriptions, retaining signals with listeners.
3. Coalesce the resulting signal/source observations, including signals reached through several watched setters.
4. Read each pending signal once, convert its value and compare it with the last processed or restored baseline.
5. Notify rules after the batch's latest values have been sampled.

Changes caused by actions wait for a subsequent pass. There is no second-frame delay merely to move from hook dispatch
to signal sampling, and no polling fallback. Initial subscription can also read when there is no saved baseline.
Expensive projections remain expensive, but provider reads and subscription checks do not belong in the postfix.

Global and building trackers own their respective saved baselines. Restore happens before saved rules bind, without
reading providers or notifying rules. Read/conversion errors preserve the last valid value; later queued reads can
recover, including notification when the error state clears without a value change. Error state is not persisted.
The external reference owns the complete save/reload and subscription behavior.

## Failure Isolation And Teardown

A registration failure removes the rejected signal's observer from all its setters. A setter patch is removed when
no other observer needs it; valid signals sharing the setter remain observed. Partially installed hook groups are
rolled back on installation failure.

A deferred source-filtering or signal-scheduling callback failure detaches that observer and removes unused patches,
preserving the original setter and registered signal definition. It is not automatically retried. This differs from
a deferred getter/conversion error, which uses normal script error handling and may recover on a later queued read.
Provider action errors likewise use normal script error reporting without unregistering the action.

Hooks and captured source references are removed on unload. If Harmony fails to remove a patch, Automation logs the
failure, clears callbacks and retains the method identity to retry removal on unload. Do not conflate cleanup failure
with permission to leave provider callbacks active across worlds.

## Validation And Documentation Handoff

Use the parent Automation instructions and the maintainer section of [AGENTS.md](AGENTS.md) for implementation gates.
Protect the postfix/dispatch boundary, same-value calls, reference identity, coalescing, saved baselines, failure
isolation and teardown in focused regression coverage after the applicable real-game validation gate.
The author-facing integration checklist remains in [HookConfiguration.md](HookConfiguration.md).

For a future Wiki guide, use the external reference as the source for choosing a source, exporting a signal/action,
editor metadata, packaging and troubleshooting. Keep the complete field reference available without requiring readers
to understand tracker, DI-registration or Harmony implementation classes. Use the shipped weather config as the real
example; the counter and Configure snippets only illustrate the schema. Do not present the old source API or a separate
weather adapter as installation requirements, imply native season rules are migrated, or claim the guide is published.
The Wiki editor retains ownership of Wiki edits and publication.
