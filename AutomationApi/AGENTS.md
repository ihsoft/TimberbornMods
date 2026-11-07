# Automation API: instructions for a consuming mod's agent

## Audience and local sources

This document travels with `AutomationApi.cs`. It is for an agent adding optional Automation support to a different
Timberborn mod. It is not a set of development rules for Automation's own repository. Follow the consuming repository's
build, style, permissions and release rules; use this file for the integration contract.

Read this file and the adjacent `AutomationApi.cs` before implementing an integration. The adjacent `README.md` is a
short human-oriented introduction. These three files are the local API bundle: no internet search, Automation source
checkout, Wiki, private definitions or knowledge of Automation's implementation is needed. Use this local bundle for
API research; do not browse for alternative signatures or integration instructions. If the helper and these instructions
disagree, report the discrepancy and inspect the supplied helper; do not invent another API or fetch a different version
silently.

## What you are building

Your mod exposes selected state through signals and selected operations through actions. Automation supplies the rule
editor, persistence, comparisons, signal observation and rule execution. Your mod owns the meaning of its values, its
gameplay logic and the moments when those values might have changed.

Both mods must work independently. Automation does not know your mod's types or require your DLL. Your mod compiles a
copy of the helper and does not reference `AdvancedAutomation.dll`, an Automation project or an Automation package.
Do not declare Automation as a required dependency in your mod manifest.

Expose a complete independent capability even when it resembles a built-in Automation capability. For example, a mod
with custom weather seasons registers its own `Season` signal and reports its own weather changes. It does not extend
Automation's built-in season list or depend on Automation's weather event handling. Two season signals in the editor
are an expected result. Automatic conflict resolution with vanilla game automation is outside this API's contract.

Keep the adapter small. Do not copy Automation's scripting types, UI definitions, registries or scheduler, and do not
write your own reflection bridge. Those details belong to the supplied helper and Automation.

## Files, dependencies and setup

1. Copy `AutomationApi.cs` into a directory compiled by your mod. Keep this `AGENTS.md` beside it for future agents.
   Preserve the source's public-domain header. Include the file exactly once in the resulting assembly.
2. The helper uses standard .NET types plus the game's `Bindito.Core`, `Timberborn.BaseComponentSystem` and
   `UnityEngine` logging. Use your project's existing game assembly references. No Automation-specific package or
   logging library is required. The supplied helper uses modern C# syntax, including file-scoped namespaces and
   target-typed `new`; the examples below also use C# 12 collection expressions. Respect the target project's language
   version rather than changing its game/framework target just for this integration.
3. Use `IgorZ.Automation.Api.AutomationApi` inside your own assembly. The helper is intentionally assembly-internal;
   each consuming assembly owns its copy. Do not make it a shared public SDK assembly.
4. Bind your integration service as a singleton in the game's `Game` context. Inject that context's `IContainer` into
   it. Pass the injected container itself to the helper, not a newly created or child container.
5. Create one helper per provider per game context. Creating it in your service constructor is fine. Declare signals
   and actions in `IPostLoadableSingleton.PostLoad`, after the state needed to describe them is available. Do not
   register capabilities in constructors, on the first tick, on opening the editor or only on the first value change.

Automation makes its endpoint available during the load phase and binds saved rules after post-load registration.
Declaring all capabilities during `PostLoad` is essential for loading rules that already reference them. Resolve any
ordering requirements between your own services using your mod's lifecycle conventions; do not introduce an arbitrary
frame delay for registration. Registration itself does not execute getters or actions.

## Complete minimal global example

This example has no dependency on a hypothetical service. Replace its in-memory counter with your existing gameplay
state and call `RecordCompletedWork` from your mod's real completion path on the game thread. It deliberately does not
introduce a save system: in a real integration, your existing service remains responsible for saving its own state.
Merge the binding into an existing Game configurator if your project already has one.

```csharp
using Bindito.Core;
using IgorZ.Automation.Api;
using Timberborn.SingletonSystem;

namespace ExampleMod;

[Context("Game")]
sealed class AutomationConfigurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<AutomationIntegration>().AsSingleton();
  }
}

sealed class AutomationIntegration : IPostLoadableSingleton {
  readonly AutomationApi _automation;
  int _completedWork;

  AutomationIntegration(IContainer container) {
    _automation = new AutomationApi(container, "ExampleMod", "ExampleMod.Name");
  }

  public void PostLoad() {
    _automation.AddSignal("CompletedWork", "ExampleMod.CompletedWork", () => _completedWork, min: 0);
    _automation.AddAction("ResetCompletedWork", "ExampleMod.ResetCompletedWork", ResetCompletedWork);
  }

  public void RecordCompletedWork() {
    _completedWork++;
    _automation.UpdateSignal("CompletedWork");
  }

  void ResetCompletedWork() {
    _completedWork = 0;
    _automation.UpdateSignal("CompletedWork");
  }
}
```

Add the referenced keys to your mod's localization resources using its normal packaging workflow. For Timberborn's
CSV localization format, the English entries for this example are:

```csv
ID,Text,Comment
ExampleMod.Name,"Example Mod","Automation provider name"
ExampleMod.CompletedWork,"completed work","Automation signal name"
ExampleMod.ResetCompletedWork,"reset completed work","Automation action name"
```

If using the game's `EventBus`, register your integration with it in `PostLoad`, handle your own relevant events with
`[OnEvent]`, and unregister in `IUnloadableSingleton.Unload`. An event handler should call `UpdateSignal` after the
underlying state changes. Ordinary C# event subscriptions likewise need matching unsubscriptions. The helper does not
subscribe to your events for you.

## Public surface

Constructor: `new AutomationApi(IContainer container, string modId, string modNameLocKey)`.

| Operation | Accepted callback | Optional metadata |
|---|---|---|
| `AddSignal(id, nameLocKey, read)` | `Func<bool>` | none |
| `AddSignal(id, nameLocKey, read)` | `Func<int>`, `Func<float>`, `Func<double>` | `min`, `max`, nullable, same numeric type |
| `AddSignal(id, nameLocKey, read)` | `Func<string>` | `options: (string Value, string NameLocKey)[]` |
| `AddAction(id, nameLocKey, execute)` | `Action` | no argument |
| `AddAction(id, nameLocKey, execute)` | `Action<bool>` | none |
| `AddAction(id, nameLocKey, execute)` | `Action<int>`, `Action<float>`, `Action<double>` | `min`, `max` |
| `AddAction(id, nameLocKey, execute)` | `Action<string>` | `options` |
| `AddPercentSignal(id, nameLocKey, read)` | `Func<float>`, `Func<double>` | `min`, `max` in fractions |
| `AddPercentAction(id, nameLocKey, execute)` | `Action<float>`, `Action<double>` | `min`, `max` in fractions |
| `ForBuildings<T>()` | `T : BaseComponent` | returns a building declaration helper |
| `UpdateSignal(id, source = null)` | no callback | omit source for globals; building component otherwise |

These are overloads, not generic value-registration methods. Use explicit lambda parameter types where overload
resolution needs help, such as `(double value) => service.SetLimit(value)`. There is no supported enum, arbitrary
struct, nullable value, collection or object signal. Convert those to a supported type yourself, and convert action
arguments back inside your own callback. Actions accept zero or one value argument.

### Numbers and booleans

Automation represents numbers with a signed 32-bit integer scaled by 100. Float and double signals round to two
decimal places, using midpoint-to-even rounding. `double` does not increase this precision. Double action callbacks
receive the fixed-point value converted directly to double, without an intermediate float conversion.

The representable script range is -21474836.48 through 21474836.47. Integer signals must also fit after multiplication
by 100, so their range is -21474836 through 21474836. Do not depend on rounding to admit values outside these limits.
NaN, infinity and values outside the scaled range are errors. An integer action rejects a fractional script argument.
Booleans appear as On/Off; in scripts, false is 0 and true is 1. A bool action interprets zero as false and any nonzero
numeric argument as true. Provider getters and action callbacks still use C# `bool`.

### Editor bounds and percentages

`min` and `max` are editor hints/constraints for numeric signal comparison thresholds and action inputs. Either bound
may be omitted. Zero is a real bound: `min: 0, max: 0` means only zero in the editor. Supplied bounds must be finite and
ordered (`min <= max`). Bounds do not clamp or reject runtime signal values or action arguments. Script expressions
can produce values outside those bounds. Enforce actual gameplay invariants in your gameplay service when needed;
do not rely on editor bounds for them. Type, option and representable-number checks still apply at runtime.

Use the separate percentage methods for percentage presentation:

```csharp
_automation.AddPercentSignal("Fill", "ExampleMod.Fill", () => service.FillRatio, min: 0, max: 1);
_automation.AddPercentAction("SetFill", "ExampleMod.SetFill",
    (double value) => service.SetFillRatio(value), min: 0, max: 1);
```

Here `service` stands for your own state/service, and its ratio must be float or double. Values, callback arguments
and bounds are all fractions: 0.75 means 75%, 1 means 100%, 2.1 means 210%. The UI displays and accepts whole percentages
with a one-percentage-point step. Scripts still use fractions, for example `Mods.ExampleMod.Fill == 1` means 100%.
There is no implicit 0..100% restriction. Request `min: 0, max: 1` when that editor range is appropriate. Ordinary
float/double methods display ordinary numbers. Do not multiply a ratio by 100 before giving it to a percentage method.

### Strings and choices

Omit `options` for a free-form string. Supply a nonempty array of `(Value, NameLocKey)` pairs for a dropdown:

```csharp
_automation.AddSignal("Mode", "ExampleMod.Mode", () => service.ModeId,
    options: [("idle", "ExampleMod.Mode.Idle"), ("working", "ExampleMod.Mode.Working")]);
_automation.AddAction("SetMode", "ExampleMod.SetMode", (string value) => service.SetMode(value),
    options: [("idle", "ExampleMod.Mode.Idle"), ("working", "ExampleMod.Mode.Working")]);
```

Values are stable, case-sensitive data; labels are localization keys. Values must be unique and non-null, and label
keys must be nonblank. A getter returning an unlisted value or an action receiving one causes a rule error. Without
options, any non-null string is accepted. Do not use translated labels as stored values. Build options from your
mod's registry before registration, and include every value the getter can return. There is no public API to replace
options or unregister a capability during the same game context; repeated registration is not an update operation.

## Building capabilities

Use `ForBuildings<T>()` when a value or action belongs to individual buildings with your component. `T` must derive
from the game's `BaseComponent`. The building also needs Automation support to host rules; declaring a capability
does not add that support to a new building.

```csharp
// During PostLoad. MyPump is your existing BaseComponent-derived type.
var pumps = _automation.ForBuildings<MyPump>();
pumps.AddSignal("Pressure", "ExampleMod.Pressure", pump => pump.Pressure, min: 0f, max: 10f);
pumps.AddAction("SetPressure", "ExampleMod.SetPressure",
    (MyPump pump, float value) => pump.SetPressure(value), min: 0f, max: 10f);
pumps.AddAction("ResetPump", "ExampleMod.ResetPump", pump => pump.ResetPump());

// After the particular pump's state may have changed:
_automation.UpdateSignal("Pressure", pump);
```

The building helper has the same supported value overloads, options, bounds and percentage methods as the global
helper. Each callback gets the resolved `T` first: `Func<T, TValue>`, `Action<T, TValue>`, or `Action<T>` for an action
without a value argument. Capability availability is determined by finding that component on the rule's building.

Pass the affected building's component to `UpdateSignal`; any component on that same GameObject identifies the same
building. Do not pass a component for a global signal, and do not omit it for a building signal. Wrong-scope updates
are ignored. To notify several buildings, notify each affected building. IDs remain provider-wide: separate building
groups and global declarations do not establish separate ID namespaces. Your custom component type is resolved by
your compiled helper; Automation does not need to reference your assembly's type.

## Notifications, ordering and performance

All registration, update, getter and action work belongs on the game thread. Deferred processing is not a thread-pool
API and provides no thread-safety guarantee. Marshal background results through your mod's existing game-thread path.

`UpdateSignal` means "this value may have changed", not "run a rule now". It never calls a getter or executes rules
on the caller's stack. Automation samples the latest state in a later rendered frame after its load activation gate.
Your method can finish updating related state before any reaction. Notifications produced by actions wait for a
subsequent processing pass. Do not assume a specific tick, exact latency or immediate callback.

Repeated notifications for the same signal/source can coalesce. Only observed sources are evaluated; without
Automation or rule listeners, notifications do not cause getter evaluation. Automation compares sampled values at its
own precision and notifies rules when they change. There is no automatic polling fallback: after registering a signal,
your mod must report possible changes. A getter must be cheap and side-effect free, since it may also be read while
initializing a rule or displaying the editor.

Call `UpdateSignal` as often as needed, including when the value may have stayed the same. For best performance, call
it as rarely as practical, using your real domain events or mutation paths instead of every tick. Do not build
elaborate duplicate-value tracking solely for Automation; it already performs the comparison. Ensure actions notify
all affected signals, preferably through the same gameplay service paths used for non-automated changes.

This is a state API, not a lossless event stream. A value that changes and changes back before sampling may produce
no reaction. Avoid one-frame boolean pulses to represent events. A counter can represent repeated occurrences as
state, but several increments can still produce one rule evaluation; it does not guarantee an action per occurrence.

## Identity, presentation and lifecycle

Choose a stable provider ID and stable capability IDs matching `[A-Za-z][A-Za-z0-9]*`. They are case-sensitive ASCII
letters/digits, beginning with a letter. Spaces, dots, underscores, colons and non-ASCII characters are invalid.
Localization keys and string option values do not have this restriction. Never encode punctuation into IDs or derive
IDs from translated display names.

The persistent script name is `Mods.<providerId>.<capabilityId>`, such as `Mods.ExampleMod.CompletedWork`. Call the
helper with the local ID (`"CompletedWork"`), not the complete script name. Renaming an ID, changing a capability's
type/scope or removing an option can invalidate saved rules. Treat those changes as compatibility work; do not make
them silently during unrelated refactoring.

The first registration for a provider ID, capability ID and kind wins. Automation logs each subsequent collision as
an ERROR and ignores it without throwing, then continues with other registrations. A signal and an action can share
an ID. Different provider IDs are independent. Global and building signals with the same provider and ID do collide.
Do not use registration order as an override mechanism. Invalid declarations (bad IDs, callbacks, bounds or options)
are programming errors and can throw even if Automation is absent; collision tolerance does not excuse those errors.

Names and option labels are localization keys resolved from your mod's resources. Keep signal/action names short for
the compact rule panel. Automation identifies the provider in its editor; do not append the mod name to every label.
An action label can include `{0}` where its argument should appear, for example `"set fill to {0}"`. Use a meaningful
provider name through `modNameLocKey`. Translating labels does not change stored IDs.

Keep the helper and callbacks owned by the current Game context. Do not cache them in a process-wide static field or
reuse them after returning to the menu and loading another world. On unload, remove your own event subscriptions.
The helper has no `Dispose` or `Disconnect` method: Automation deactivates its old endpoint, and your new Game context
creates a new helper and registers its capabilities again.

## Optional connection and diagnostics

The helper lazily searches loaded assemblies for the versioned `Automation.ConnectV1` entrypoint. Use the supplied
helper rather than implementing its internal reflection/delegate protocol. No manual "is Automation installed?"
guard is needed around valid declarations or notifications. Keep your gameplay operational when the endpoint is absent.

If the endpoint method is missing, it emits this INFO message once per helper instance:

```text
[Automation API] Mod 'ExampleMod' supports Automation.ConnectV1, but the endpoint was not found.
```

This is also expected when Automation is not installed. With Automation installed, it helps diagnose an incompatible
version or an unloaded/wrong assembly. Multiple helpers can each produce one message; repeated calls on the same
helper do not repeatedly search or log a missing method. If the method exists but the Game context is not active yet,
the helper can retry connecting on later calls; that is distinct from a missing endpoint method. Follow the PostLoad
contract rather than relying on retries to repair late registration.

| Symptom | Check locally first |
|---|---|
| Capability absent from the editor | integration binding, PostLoad execution, endpoint message, duplicate errors, localization resources |
| Visible on the wrong buildings or absent on custom buildings | global versus building declaration, matching component, building's Automation support |
| Rule fails after loading a save | stable IDs/options/type/scope, registration completed in PostLoad, correct Game container |
| Value changes without a reaction | UpdateSignal path, exact local ID/case, source scope, active listeners, fixed-point rounding/coalescing |
| Percentage off by a factor of 100 | fraction contract for getters, arguments and bounds |
| Old value never refreshes after another world loads | static or reused helper, stale subscriptions/callbacks from previous Game context |
| Script can pass a value outside min/max | expected editor-only bounds; enforce gameplay requirements in the owning service |
| Duplicate error | repeated registration, reused provider ID, or provider-wide collision across building groups |
| Runtime value error | null/unknown option, unrepresentable number, fractional integer action, exception in your gameplay callback |

## Integration validation and handoff

Use your repository's normal local build and packaging commands; no machine-specific path or Automation repository
tool is required by this API. Inspect the final project and package dependencies to confirm that optional integration
has not introduced an Automation DLL reference or required manifest dependency.

Validate the relevant cases in game, following the consuming repository's validation policy:

1. With Automation absent, the mod loads and its normal gameplay works. The single missing-endpoint INFO is expected.
2. With a compatible Automation version, capabilities appear with correct names, provider attribution and types.
3. Real state changes trigger rules after notification. Unchanged/redundant notifications do not cause spurious
   change reactions. Actions work and notify their resulting state changes.
4. For building capabilities, use two buildings to check source isolation and component availability.
5. Check boolean choices, string options, numeric precision and any percentage/boundary inputs actually exposed.
   Where applicable, test zero bounds and one-sided bounds. Runtime gameplay limits need their own checks.
6. Save a rule, reload the world, and verify that it binds and continues reacting. Return to the menu and load another
   world to check context ownership and subscription cleanup.
7. Review logs for missing endpoints with Automation present, duplicate IDs and callback/value errors. Ensure the
   package actually tested is the one built from the current integration.

Report what was built and what was actually tested. A successful compile or a test with a mock helper does not prove
game lifecycle ordering, UI layout or save restoration. Do not add permanent test/demo capabilities to the shipped
mod merely to exercise all API overloads; keep only the capabilities requested for its real gameplay.
