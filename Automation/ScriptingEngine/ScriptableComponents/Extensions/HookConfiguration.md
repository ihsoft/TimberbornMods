# Automation hook JSON format, version 1

This is the technical reference for the implementation in this directory. It describes the current experimental
branch; it does not claim that the feature is available in a published Automation release. It is source material for
the human-facing Wiki guide and a local reference for agents authoring configurations. No web lookup is needed to
understand the configuration contract. Examples using `ExampleMod` are illustrative, not shipped test integrations.

For the actual schema and validation, see [HookConfiguration.cs](HookConfiguration.cs),
[ConfiguredIntegrations.cs](ConfiguredIntegrations.cs), and [HookValueFactory.cs](HookValueFactory.cs).

Expose existing mod properties and methods through JSON configuration, without compiling Automation code into the
provider. Automation discovers the config, builds native signal and action definitions, observes setters and invokes
actions. No registration or update calls are needed in the provider's code.

## Installation and ownership

Place one or more JSON files with the `AutomationHooks` filename prefix in your mod's selected version directory,
alongside its manifest (not inside Scripts). For example: `YourMod/version-1.1/AutomationHooks.ModdableWeathers.json` and
`AutomationHooks.OtherMod.json`. The original `AutomationHooks.json` name also works. Prefix and `.json` extension
matching are case-insensitive. Include localization keys in the mod's normal localization files.
For an unversioned mod, use the mod directory itself. For a versioned mod, put the file in each supported version's
directory; files in the outer mod root or another version's directory are not combined with the selected version.
No Automation DLL, helper source, registration code or update calls are required. Do not require Automation in your
manifest; without Automation, the files are simply unused. A separate compatibility mod can describe another loaded mod's
types and provide its own labels. It needs no integration code in its DLL.

Automation can also ship separate configuration files for other mods, without changes to those mods. If a named
assembly is not loaded, the affected target is skipped with an INFO message. This does not require installing the mod
or adding it as a dependency. An absent OptionsSource assembly similarly skips only its dependent signal or action.

Automation scans each enabled mod's version directory, including its own, in the loader's order, once per world load.
Within each mod, matching files are processed in ordinal filename order. Each file is loaded independently; a malformed
file does not prevent the next one from loading. Disabled mods are not scanned. There is no recursive search, tick
polling or live file watching. Reload the world after editing files. Each file uses the object format below
(`Version: 1`). The containing mod need not be the mod named by a target's Assembly.

## Object reference

Use the property spelling shown here. CLR names, IDs, scope names and supported type names are case-sensitive.
Use normal JSON strings, numbers, booleans, arrays and objects; do not encode booleans or numbers as strings. Unknown
properties reject the entire file, including an unknown property inside a nested declaration. There is no expression
language, include directive, environment-variable expansion or CLR `$type` activation. Maximum JSON nesting depth is
32. The top-level value must be an object.

Defaults apply to omitted fields. Prefer omission over explicit `null`; the tolerated null cases below are not extra
features. Empty arrays contain no declarations. Null array elements are invalid entries, not placeholders.

### File and provider

| Object | Property | Required/default | Contract |
| --- | --- | --- | --- |
| File | `Version` | Required | Integer `1`; any other version rejects the file. |
| File | `Providers` | Defaults to `[]` | Array of provider objects. Explicit `null` rejects the file. |
| Provider | `Id` | Required | Stable namespace segment matching `[A-Za-z][A-Za-z0-9]*`. Also shown verbatim as the editor origin. |
| Provider | `Targets` | Defaults to `[]` | Array of target objects. `null` is treated as empty. |

There is no provider `NameLocKey` or separate provider display name. A provider can contain targets from several
assemblies; separate files may add disjoint capabilities under the same provider ID. A provider ID is not a DLL name,
manifest ID or automatic dependency declaration.

### Target and source identity

| Property | Required/default | Contract |
| --- | --- | --- |
| `Assembly` | Required | Loaded assembly's simple name, without `.dll`, path or version-qualified assembly syntax. |
| `Type` | Required | Full CLR type name in that assembly. Nested CLR types use `Outer+Inner`. The source must be a closed reference type. |
| `Scope` | Defaults to `"Singleton"` | Exactly `Singleton`, `Building` or `Static`; explicit `null` is invalid. |
| `Signals` | Defaults to `[]` | Array of signal objects; `null` is treated as empty. Only present on targets. |
| `Actions` | Defaults to `[]` | Array of action objects; `null` is treated as empty. Only present on targets. |

An options source uses the same `Assembly`, `Type` and `Scope` fields, with additional fields described below. It has
neither `Signals` nor `Actions`. Automation selects the already loaded assembly by exact simple name, then calls
`Assembly.GetType` for the exact type name. It does not enumerate all game types or load another DLL from disk.
Duplicate loaded assemblies with the requested simple name are an error, not an arbitrary choice.

### Signal

| Property | Required/default | Contract |
| --- | --- | --- |
| `Id` | Required | Stable capability segment with the same restrictions as provider IDs. |
| `NameLocKey` | Required | Non-blank localization key for the signal label. |
| `Read` | Required | Non-blank property/method path whose result is one supported value type. |
| `Watch` | Defaults to the first segment of `Read` | Array of setter property names on the target type. `null` also selects the default; `[]` installs no notifications. |
| `Value` | Defaults to `{}` | Value metadata object. Explicit `null` is invalid. The type is inferred from `Read`. |

For `Read: "GetTotal()"`, the default watch would not name a property. Supply an explicit `Watch`, such as
`["Count", "Bonus"]`. For `Read: "CurrentStage.Stage.Weather.Id"`, the default watches `CurrentStage` only.

### Action

| Property | Required/default | Contract |
| --- | --- | --- |
| `Id` | Required | Stable capability segment with the same restrictions as provider IDs. |
| `NameLocKey` | Required | Non-blank localization key; the localized text can use `{0}`, `{1}`, etc. for arguments. |
| `Method` | Required | Method name on the target type, without parentheses or a member path. Must return `void`. |
| `Arguments` | Defaults to `[]` | Ordered value metadata objects. Each requires `Type`. Explicit `null` is invalid. |

The number and order of `Arguments` define the exact CLR signature, not just which controls appear in the editor.
Optional CLR parameters must still be described and supplied; no optional argument omission or `params` expansion is
performed. There is no asynchronous-method adapter or generic-method specialization.

## Minimal complete example

Assume `Example.CounterService` is a Game singleton in ExampleMod.dll, with an int Count property and `void Set(int)`.

```json
{
  "Version": 1,
  "Providers": [{
    "Id": "ExampleMod",
    "Targets": [{
      "Assembly": "ExampleMod",
      "Type": "Example.CounterService",
      "Signals": [{
        "Id": "Count",
        "NameLocKey": "Example.Automation.Count",
        "Read": "Count",
        "Value": { "Min": 0, "Max": 100 }
      }],
      "Actions": [{
        "Id": "SetCount",
        "NameLocKey": "Example.Automation.SetCount",
        "Method": "Set",
        "Arguments": [{ "Type": "int", "Min": 0, "Max": 100 }]
      }]
    }]
  }]
}
```

Example English localization records:

```csv
ID,Text,Comment
Example.Automation.Count,"count","Current counter value"
Example.Automation.SetCount,"set count: {0}","Action and its argument in compact rule descriptions"
```

Provider and capability IDs are case-sensitive and must match `[A-Za-z][A-Za-z0-9]*`. Script names are
`Mods.ProviderId.CapabilityId`. Preserve these IDs when updating a mod: saved rules use them. Localization keys and
string option values do not have that identifier restriction. Signal, action and option names are localized. The editor
shows the provider's Id verbatim as the origin; providers do not declare a NameLocKey. The compact building panel keeps
the capability's own label. Actions may use `{0}`, `{1}`, etc. in their labels to display arguments. A capability may
reuse an existing Automation localization key, such as `IgorZ.Automation.Scriptable.Weather.Signal.Season`.

The first successful registration of a provider/capability/kind wins across all JSON files.
Later duplicates are logged and ignored. A signal and an action may share an ID. JSON order is preserved within a file;
do not use registration order as an override mechanism.

## Sources and value paths

Each target specifies its assembly's simple name without `.dll`, full type name, and optional Scope:

| Scope | Source | Signal/action availability |
| --- | --- | --- |
| `Singleton` (default) | Resolved from the Game DI container after singleton Load calls | Global |
| `Building` | A BaseComponent on the rule owner's GameObject | Only buildings with that component |
| `Static` | Static members on the named type | Global |

Automation does not create provider bindings or attach building components. The provider owns those normally.
Building values remain independent between entities. Signals can be exported according to their normal Automation
scope; global declarations are not building exports.

Signal `Read` is a dot-separated path of readable properties and parameterless methods, e.g.
`CurrentStage.Stage.Weather.Id` or `GetPressure()`. Methods must be explicitly written with `()` and be side-effect free.
Nested reference values are null-safe during observation; an unavailable/null final signal value still produces the
normal script value error when evaluated. Plain fields, indexers, method arguments and arbitrary expressions are not
part of the path syntax. The final result must be bool, int, float, double or string. Convert enums and custom structures
in provider-owned getters when needed. Static scope applies to the first path member; later members belong to its result.

`Watch` is an optional array of property names on the target class. Automation patches their managed setters at runtime.
When omitted, it watches the first property in Read. Examples:

```json
{ "Id": "Total", "NameLocKey": "Example.Total", "Read": "GetTotal()", "Watch": ["Count", "Bonus"] }
```

```json
{ "Id": "Season", "NameLocKey": "Example.Season", "Read": "CurrentStage.Stage.Weather.Id" }
```

Use `Watch: []` only for values that need no change notification. Get-only properties can be read, but cannot themselves
be watched. A nested object's independent mutations require a watched setter on the target to surround that mutation;
merely assigning the same outer value afterward will not announce a change already completed. Replacing
the outer property is observed automatically. Multiple signals may watch the same setter, and one signal may watch
multiple setters. Private setters are supported. Inlined calls and non-managed/native setters may prevent interception;
validate the actual target in game. Patches are installed before singleton Load, then activated after registration.

The observer reads before/after only when the signal has listeners. An unchanged raw CLR result does not queue an update.
It compares the `Read` projection, not the setter's argument. Getters must be cheap, side-effect free and safe both before
and after every watched setter. A getter which depends on state updated after that setter may miss the eventual change;
choose a different setter or a projection whose change is visible inside the observed call.
Changed results enter Automation's deferred queue, processed with WaitForEndOfFrame after Automation is ready.
Rules never execute inside the setter. Repeated notifications are coalesced, the latest value is sampled, and changes
caused by actions wait for a subsequent pass through the same end-of-frame mechanism.
There is no guarantee to deliver intermediate states; use a counter to represent repeated occurrences.
All configured members must be used on the game thread. No polling fallback is installed.

## Value metadata

Signal Value is optional; its CLR type comes from Read. Actions require Type for each argument so overload selection is
exact. If Type is also supplied for a signal, it must match the getter's result.

| Field | Required/default | Meaning |
| --- | --- | --- |
| `Type` | Required for arguments; inferred for signals | Exactly `bool`, `int`, `float`, `double` or `string`; a supplied signal type must match exactly. |
| `NameLocKey` | Omitted/null | Optional argument label; useful for actions with several arguments. A supplied key cannot be blank. Does not replace the signal's own label. |
| `Min`, `Max` | Omitted/null | Inclusive numeric editor bounds; either side can be omitted; `Min: 0, Max: 0` means only zero. Allowed only for int/float/double. |
| `Percent` | `false` | `true` for float/double fractions displayed as whole percentages. Invalid for bool/int/string. |
| `Options` | Omitted/null | Non-empty array of option objects; string values only. Mutually exclusive with `OptionsSource`. |
| `OptionsSource` | Omitted/null | Source object providing string options; described below. |

Bool is shown as On/Off and maps to script numbers 1/0; when an action receives a script number, zero means false and
any nonzero value means true. Float and double use Automation's two-decimal fixed-point precision; double does not add
precision. Getter numbers are multiplied by 100 and rounded to the nearest integer, ties to even. The scaled value must
fit a signed 32-bit integer: the represented range is -21474836.48 through 21474836.47. Int signals therefore accept a
smaller range than a CLR int. Int action arguments must have no fractional part; there is no truncation.
NaN, infinity, null final values, nullable types, long, decimal, enums and custom structures are unsupported.
Convert unsupported results in a provider-owned property or parameterless method. Percentage data and bounds use fractions:
0.75 means 75%, and Min 0 / Max 1 gives 0..100% in the editor. Without bounds, negative and above-100% values are allowed.
Min/Max apply to action inputs AND signal comparison thresholds in the editor. They do not clamp or reject runtime
values, including values entered through scripts. Bounds must be finite and ordered. Strings may be free text or use
unique options; configured options are also validated at runtime. A raw numeric change which rounds to the same script
value produces no rule notification. The percent step is one percentage point; there is no automatic 0..100% bound.

Each fixed option has exactly two fields: `Value` is a required, non-null string (the empty string is allowed), and
`NameLocKey` is a required, non-blank localization key. Values must be unique with case-sensitive string equality.
Labels may coincide. Array order is preserved. An empty options array is invalid; omit it for a free-text string.

For dynamic dropdowns, OptionsSource specifies another Singleton or Static source plus a Read path returning
`IEnumerable<T>`. ValuePath and NameLocKeyPath are property/method paths on each item and must return strings:

| Options source field | Required/default | Contract |
| --- | --- | --- |
| `Assembly`, `Type` | Required | Same exact source identity as a target. |
| `Scope` | `"Singleton"` | `Singleton` or `Static`; `Building` is invalid because the option list is shared. |
| `Read` | Required | Path returning a non-null `IEnumerable<T>`; arrays and generic collections are supported. A non-generic `IEnumerable` alone is insufficient. |
| `ValuePath` | Required | Property/method path on T, returning the stored string. |
| `NameLocKeyPath` | Required | Property/method path on T, returning the localization key. |

```json
{
  "OptionsSource": {
    "Assembly": "ModdableWeathers",
    "Type": "ModdableWeathers.Weathers.ModdableWeatherRegistry",
    "Read": "Weathers",
    "ValuePath": "Id",
    "NameLocKeyPath": "Spec.NameLocKey"
  }
}
```

Options are captured during PostLoad, after registries have loaded, and retain registry order. Entries without a name
key are skipped. Do not specify both Options and OptionsSource. Per-building or continuously changing option lists
are not supported by this shared editor metadata. Retained entries must satisfy the same validation as fixed options:
non-null, unique values and at least one visible entry. A null/empty/whitespace name key hides the entry before its
value is read. Localization is resolved at registration; existence of a translation is not a separate schema check.

## Actions

Method identifies a void method on the target. Arguments is an ordered array with any number of supported parameters;
omit it or use `[]` for no arguments. Each argument has independent names, bounds, percent formatting and options.
Overloads are selected by the exact declared parameter types; ref/out, open generic methods and params-array expansion
are not supported. A setter can also be invoked by its CLR name, e.g. `set_Count` with one int argument.

```json
{
  "Id": "Configure",
  "NameLocKey": "Example.Configure",
  "Method": "Configure",
  "Arguments": [
    { "Type": "bool", "NameLocKey": "Example.Enabled" },
    { "Type": "double", "NameLocKey": "Example.Fill", "Percent": true, "Min": 0, "Max": 1 },
    { "Type": "string", "NameLocKey": "Example.Label" }
  ]
}
```

This calls `void Configure(bool, double, string)` with converted values in that order. The existing rule editor renders
one named control per argument and preserves the full list in saved scripts. Actions may modify state normally;
configured setter observers detect resulting changes. Provider code must still own its normal state persistence.

Automation separately saves the last processed signal values: one value per world for global signals and one per
building for building signals. Saved values are restored before rules are bound, without reading the source or
notifying rules. The next observed setter change queues a deferred read and comparison with that saved baseline.
Signals without a saved value are initialized from their source when first observed. A read error preserves the last
valid value; recovering on a subsequent queued read notifies rules even if that value has not changed. Error state itself
is not persisted. Removing the last listener drops an actively tracked baseline; a later subscription initializes again.
`Watch: []` does not keep a changing value fresh: subscribed reads use the cached baseline. These signals are suitable
only when no change notification is needed. Saving Automation's baseline does not save or restore the provider's state.

## Errors and lifecycle

Missing config files are normal. Missing target or options-source assemblies produce an INFO skip message without an
exception stack. A loaded assembly with a missing type, property or method remains an ERROR: the declaration is invalid
or incompatible with that mod version. Unsupported versions, malformed files and invalid declarations are logged; a bad
file or entry does not abort world loading or prevent independent entries from registering. IDs, result types, bounds,
fixed options and registry access paths are validated before installing signal patches. Actual singleton resolution,
registry contents and registration conflicts are checked after loading. A failure at that stage removes the rejected
signal's observer from all its setters. A setter patch is removed when no other observer needs it; valid signals sharing
the setter remain observed. Partially installed hook groups are rolled back on installation failure.

Runtime observer failures also detach the failing observer and remove unused patches, while preserving the original
setter. Provider action/getter errors are handled by Automation's normal script error reporting. Hooks and captured
source references are removed on unload. If Harmony itself fails to remove a patch, Automation logs that failure,
clears its callbacks and retains the method identity to retry removal on unload.

These failure paths are different:

| Failure | Result | Recovery |
| --- | --- | --- |
| No matching config file | Nothing registered; normal situation. | Install a config and reload the world. |
| Target assembly absent | INFO, skip that target and its capabilities. | Enable the target mod and reload. |
| Options-source assembly absent | INFO, skip the dependent signal or action. | Enable the registry's mod and reload. |
| JSON parse/version/unknown property error | ERROR, reject the whole file. Other files still load. | Fix the file and reload. |
| Bad target identity/scope or unavailable singleton binding | ERROR, skip that target. | Fix identity/binding and reload. |
| Invalid signal/action metadata or duplicate registration | ERROR, skip that capability; keep independent valid entries and the first successful duplicate. | Fix the declaration and reload. |
| Reader/observer callback throws around a setter | ERROR, disable that observer and remove its unused patches. The signal definition remains registered. | Fix the getter/hook and reload; no automatic retry of the disabled observer. |
| Deferred signal read or conversion fails | Script error; keep the last valid baseline. Other signals continue. | A later observed change can queue another read and recover. No periodic retry. |
| Action throws or receives an invalid typed/option argument | Normal script runtime error; the action stays registered. | A later execution may succeed. Editor bounds are not runtime validation. |

A configuration cannot make a throwing original setter safe or intercept every possible write. Setter exceptions,
direct backing-field writes, inlined calls and background-thread writes are outside the notification contract.

Registration finishes in PostLoad before Automation binds saved rules. Configs should describe state/registries ready
after singleton Load; late registration or arbitrary delays are not used. Test fresh load, save/reload, exact target
signatures, per-building isolation and real changes before relying on an integration.

## Built-in Moddable Weathers integration

[AutomationHooks.ModdableWeathers.json](../../../AutomationHooks.ModdableWeathers.json) ships with Automation and
exposes `Mods.ModdableWeathers.Season`. It observes the CurrentStage setter, reads the current weather ID and obtains
dropdown choices from the weather registry. The signal reuses Automation's existing season label; its editor origin
is `ModdableWeathers`.

Enable Automation and Moddable Weathers; no separate integration mod is needed. Without Moddable Weathers, the config
is skipped with an INFO message. Automation's built-in Weather.Season support remains unchanged. Existing rules using
Mods.ModdableWeathers.Season keep their identity; rules using the legacy extended Weather.Season are not migrated.

## Implementation and validation map

| Responsibility | Source |
| --- | --- |
| JSON properties/defaults | [HookConfiguration.cs](HookConfiguration.cs) |
| File discovery, early validation, registration, options | [ConfiguredIntegrations.cs](ConfiguredIntegrations.cs) |
| Assembly/type identity, DI and building ownership | [ConfiguredTarget.cs](ConfiguredTarget.cs) |
| Compiled getters/actions and setter lookup | [HookMemberAccess.cs](HookMemberAccess.cs) |
| Shared patches, before/after comparison, removal | [PropertySetterHooks.cs](PropertySetterHooks.cs) |
| Native editor metadata, types, numbers and runtime validation | [HookValueFactory.cs](HookValueFactory.cs), [HookValueConverter.cs](HookValueConverter.cs) |
| End-of-frame queue and scripting adapter | [ExternalScriptableComponent.cs](ExternalScriptableComponent.cs) |
| World/building saved baselines | [AbstractGlobalStatusTracker.cs](../Components/AbstractGlobalStatusTracker.cs), [AbstractStatusTracker.cs](../Components/AbstractStatusTracker.cs) |

Reflection and expression compilation happen during world initialization. Setter callbacks use compiled delegates;
they do not search for types or members again. Each watched setter incurs the cheap observer/listener checks even when
it produces no update; expensive getter projections are still expensive and should be avoided. The deferred queue
coalesces pending observations per signal/source and reads their latest values before notifying any rule in that batch.

For a new declaration, establish the exact installed target version, assembly/type identity, method signatures, DI
binding or component ownership, and which setter encloses the observable mutation from local provider source or DLL
metadata. Do not guess them from display names. Check the JSON against every field above and provide the referenced
localization entries. Validate in game: config discovery and absence of registration errors; signal/action availability;
each argument control and its units; real changes, repeated same-value writes, and action-driven changes; save/reload
without false transitions; independent building instances; absence of the optional target mod. A successful parse or
compile is not evidence that a runtime setter was intercepted.

For the Wiki editor: organize the human guide around choosing a source, exporting one signal, exporting an action,
adding editor metadata, packaging the file and troubleshooting it. Keep the complete field reference available, but
do not require the reader to understand internal tracker, DI registration or Harmony implementation classes. Explain
the observable restrictions and deferred value-change behavior. Use the shipped weather config as the real example;
the counter and Configure snippets above only illustrate the schema. Do not present the old source API or a separate
weather adapter as installation requirements, and do not imply that existing native season rules are migrated.
