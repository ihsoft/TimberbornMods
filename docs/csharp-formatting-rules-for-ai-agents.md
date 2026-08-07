# C# Formatting Rules for AI Agents

This document defines the preferred C# formatting style for this project/user. It is intended primarily for AI coding agents. When generating or modifying C# code, follow these rules unless explicitly instructed otherwise.

## Core style

The style is close to Google Java Style, adapted for C#.

Goals:

  * Compact code.
  * Consistent formatting.
  * Minimal vertical expansion.
  * High readability.
  * Avoid typical C# Allman-style formatting.

When in doubt, prefer the more compact version as long as it remains readable.

* * *

## Indentation

Use 2 spaces for normal executable code indentation.

    if (condition) {
      DoSomething();
    }

Do not use 4 spaces for normal code blocks.

    if (condition) {
        DoSomething();
    }

* * *

## Braces

Use K&R / Java-style braces.

    class Example {
      void Method() {
        DoSomething();
      }
    }

Do not use Allman braces.

    class Example
    {
      void Method()
      {
        DoSomething();
      }
    }

* * *

## Namespace

Prefer file-scoped namespaces.

    namespace MyProject.MyFeature;

Every non-generated C# source file should use the project's intended namespace unless the surrounding project has an
explicit, established reason to use the global namespace. Do not leave new or reorganized production and test types in
the global namespace by omission.

When creating or moving files between feature folders, verify that their namespaces still match the project's
established namespace and feature organization. A filesystem move does not update the C# namespace automatically.

When establishing or normalizing namespaces across a project, inspect the project file as part of the same change.
Verify that `RootNamespace` is present and agrees with the intended common namespace root; add or correct it when the
project would otherwise derive an inconsistent root from its project or assembly name.

* * *

## Using directives

Remove unused `using` directives whenever a C# file is changed. Do not leave obsolete imports, aliases, or former
nested-type imports for a later cleanup pass.

Include the complete `using` block in the final per-file audit after moving types, changing namespaces, or extracting
nested types.

* * *

## Application entry points

Executable C# applications must keep all application functionality in named classes. Do not implement an application
as top-level statements or leave operational code directly at module/file scope.

Use one of these explicit entry-point shapes:

  * an application class with a static `Main()` method, or
  * a small entry-point/loader class whose `Main()` constructs or invokes the class that owns the application.

Keep a separate loader thin. Argument handling may begin there, but substantive workflow, state, services, helpers,
DTOs, and business logic belong to the application or feature classes. A short executable does not justify replacing
the class structure with top-level statements.

* * *

## Access modifiers

Use access modifiers to express type and module ownership. Do not minimize them mechanically.

Leave application-owned top-level types implicitly `internal`; do not write the redundant `internal` modifier. Declare
a top-level type `public` only when it forms an intentional API for consumers outside the assembly.

Within an internal application type, use `public` members for the contract used by other application modules. Use
`internal` on a member only when a specific assembly-level technical boundary requires it. Keep implementation details
owned by one class private or omit the modifier when the language default expresses the intended privacy.

For example:

    sealed class MapArchiveAnalyzer {
      public void Analyze(string path) {
        AnalyzeArchive(path);
      }

      void AnalyzeArchive(string path) {
      }
    }

Harmony patch classes and methods do not require explicit access modifiers unless technically necessary.

* * *

## `var`

Prefer `var` when the type is obvious from the right-hand side.

    var button = new Button();
    var count = modItems.Count;

Use explicit types only when they improve readability or the type is not obvious.

* * *

## Line length

Maximum line length is 120 characters.

If a statement fits within 120 characters, keep it on a single line.

Prefer:

    var visible = !showOnlyActive || ModPlayerPrefsHelper.IsModEnabled(mod);

Avoid unnecessary wrapping:

    var visible =
      !showOnlyActive ||
      ModPlayerPrefsHelper.IsModEnabled(mod);

* * *

## Line wrapping philosophy

Wrapping is not used to improve aesthetics.

Wrapping is used only when:

  * The line exceeds 120 characters.
  * The code becomes genuinely difficult to read.

If code fits within 120 characters, keep it on one line.

* * *

## Broad style refactors

Before a project-wide or otherwise broad C# style refactor, enumerate the exact target `.cs` files and audit each file
at the code-construct level. Do not validate style only through whitespace heuristics such as odd indentation or line
length.

The audit should cover the repository style contracts that are easy to miss during mechanical cleanup:

  * source header,
  * namespace and project `RootNamespace`,
  * unused imports and aliases,
  * explicit application class and entry point for executable projects,
  * top-level, helper, DTO, and nested-type ownership and placement,
  * domain-specific names for shared types,
  * field and constant placement,
  * access modifiers that express application and module boundaries,
  * wrapped declarations, calls, expressions, and collection initializers,
  * raw string literals,
  * final line length.

Build a type inventory as well as a file inventory. A DTO or helper used only by the class that creates and owns it
should normally be nested in that owner instead of remaining as an unrelated top-level type. Place nested types near
the top of the owning class, after important class-level constants and fields, so the class structure is visible before
its behavior.

A DTO or container consumed by multiple application modules has independent ownership and should normally be a
top-level type, even when only one module creates it. Give shared top-level containers domain-specific names that remain
meaningful outside the producer and do not collide with unrelated concepts. Preserve the project's established domain
vocabulary; examples illustrate the naming principle and must not be promoted into required identifiers.

If files or folders move during the refactor, re-check every affected namespace and the project's `RootNamespace`
against the final organization. Compilation proves that names resolve; it does not prove that types were left in the
intended architectural namespace.

Do not run `dotnet format` or another generic formatter unless its effective configuration has first been verified to
produce this repository's K&R braces, 2-space block indentation, 4-space continuation indentation, and compact wrapping.
If no compatible `.editorconfig` or tool configuration exists, do not use formatter defaults.

Never apply mechanical indentation rewrites across raw string literals. If a formatter or rewrite touches raw strings,
inspect those regions carefully or restore them before continuing.

After the user identifies a missed existing style rule, re-read the applicable style document and restart a complete
audit of the requested scope. Do not fix only the examples named by the user when the same rule may have been missed in
other files.

When minimizing access modifiers on nested types, compile immediately after the modifier pass. Do not assume containing
and nested C# types can freely use each other's private members in every direction.

Before handoff, verify the exact diff scope so formatting tools have not modified linked, shared, or unrelated files
outside the target set.

* * *

## Constants and local literals

Do not hoist every literal into top-of-class constants by habit.

Use class-level constants for values that are important contracts or durable configuration:

  * Localization keys.
  * Save keys.
  * Settings keys or default names.
  * Mod-wide default values.
  * Shared tuning values.
  * Values used across multiple methods where the name clarifies behavior.

Do not replace incidental literals with local constants by habit either.

For visual component initialization, prefer direct literals when the target variable, property, or nearby method already
gives enough context. For example, a value assigned inside `saveVersionLabel.style.marginTop = 4` usually does not need
a separate `SaveVersionLabelMarginTop` constant.

Use a local constant only when the value is reused in the method or when the constant name adds real meaning that the
surrounding code does not provide.

If a one-off literal needs semantic explanation, prefer a short comment over an artificial constant name.

Top-of-class constants should make important contracts easy to find. Local constants should clarify real local concepts.
Neither should turn incidental UI glue into fake configuration.

* * *

## Wrapped method calls

When a method call exceeds 120 characters, preserve as much of the call signature as possible.

Do not split:

  * Method name.
  * Generic parameters.
  * Member access chain.

Instead, wrap only the argument list.

Preferred:

    var modItems = AccessTools.FieldRefAccess<ModListView, Dictionary<Mod, ModItem>>(
        modListView,
        "_modItems"
    );

Avoid:

    var modItems = AccessTools.FieldRefAccess<
        ModListView,
        Dictionary<Mod, ModItem>>(
        modListView,
        "_modItems"
    );

Avoid:

    var modItems = AccessTools
        .FieldRefAccess<ModListView, Dictionary<Mod, ModItem>>(
            modListView,
            "_modItems"
        );

### Argument indentation

Wrapped argument lists use 4 spaces.

    SomeMethod(
        firstArgument,
        secondArgument,
        thirdArgument
    );

Rationale:

Arguments are part of a declaration-like structure rather than executable logic. Using 4 spaces visually distinguishes them from normal code blocks.

### Compact wrapped arguments

Default to the fewest readable lines.

When formatting arguments or parameters, first try to keep multiple arguments on the same line. Split one argument per
line only when the combined line would exceed the 120-character limit or when grouping them clearly hurts readability.
Do not force one argument per line by habit.

This rule applies to method calls, method declarations, constructors, and primary constructors.

This includes constructor calls and object creation expressions such as `new SomeSetting(...)`. Keep short literal or
configuration arguments together on one line when they fit cleanly, especially numeric ranges, flags, and small option
groups.

For callback registration and similar small two-argument calls, do not split each argument onto its own line by habit.
If the call receiver and opening parenthesis need their own line and the arguments fit comfortably on the continuation
line, keep the short value argument and short lambda together.

Preferred:

    InstallSettingCallback(
        EnhanceSaveModsIncompatibilityDialogInternal, v => EnhanceSaveModsIncompatibilityDialog = v);

Avoid:

    InstallSettingCallback(
        EnhanceSaveModsIncompatibilityDialogInternal,
        v => EnhanceSaveModsIncompatibilityDialog = v);

Preferred:

    static WeatherScriptableComponent CreateComponent(
        AutomationExtensionsRegistry registry = null, WeatherService weatherService = null,
        HazardousWeatherService hazardousWeatherService = null) {
      ...
    }

Also acceptable when it improves readability:

    static WeatherScriptableComponent CreateComponent(
        AutomationExtensionsRegistry registry = null,
        WeatherService weatherService = null,
        HazardousWeatherService hazardousWeatherService = null) {
      ...
    }

Prefer the fewer-line version when both are readable.

After adding, removing, or reordering constructor or method parameters, re-check whether the final signature can be
formatted more compactly while staying readable and within the 120-character limit.

After extracting string literals, renaming identifiers, or otherwise changing expression length, re-check nearby
wrapping semantically. A maximum-line-length check only finds lines that are too long; it does not find obsolete wraps
that became unnecessary after the expression got shorter. Collapse formerly necessary wrapping when the final code now
fits cleanly on one readable line.

### `out` and `ref` parameters

Avoid `out` and `ref` parameters in helper methods when they are used only to expose temporary local state.

If an `out` or `ref` parameter is genuinely needed, place it at the end of the parameter list.

* * *

## Wrapped expressions

When an expression exceeds 120 characters, wrap it only when necessary.

Wrapped expression continuations use 4 spaces, not 2 spaces.

Rationale:

A multi-line expression is still one value computed as a single unit. It is closer to initialization/declaration than to multiple executable statements.

### Operators move with the right operand

When wrapping an expression, keep the operator together with the operand/expression that follows it.

Preferred:

    var enabled = hasManualOverride
        || ModPlayerPrefsHelper.IsModEnabled(mod) && !ModPlayerPrefsHelper.HasModWarning(mod);

Avoid:

    var enabled = hasManualOverride ||
        ModPlayerPrefsHelper.IsModEnabled(mod) && !ModPlayerPrefsHelper.HasModWarning(mod);

### Preserve logical execution groups

When wrapping boolean expressions, keep natural logical groups together according to operator precedence and intent.

For example, in `var1 || var2 && var3`, `var2 && var3` is the logical group.

Preferred:

    var result = var1
        || var2 && var3;

Avoid:

    var result = var1 || var2
        && var3;

The second form visually suggests the wrong grouping.

### Do not wrap if it fits

Prefer:

    var result = var1 || var2 && var3;

Avoid:

    var result = var1
        || var2 && var3;

if the single-line version fits within 120 characters.

* * *

## Pattern matching readability

Use pattern matching when it stays readable.

If a property, list, or nested pattern inside an `if (... is ... { ... } variable)` becomes difficult to scan, split it
into a simple type check first and then write separate property checks.

Preferred for complex cases:

    if (value is not SomeType typedValue) {
      return;
    }

    if (typedValue.Items is not [var firstItem, ..]) {
      return;
    }

    if (!firstItem.IsEnabled) {
      return;
    }

Avoid compact pattern matching that hides several decisions inside one condition just to save lines.

* * *

## Object and collection initializers

Object and collection initializers use 4 spaces.

    var filterButton = new Button() {
        name = "ShowActiveModsButton",
        text = "Show active",
    };

    var values = new List<int>() {
        1,
        2,
        3,
    };

Use trailing commas in multi-line initializers.

* * *

## Lambdas

Lambda bodies are normal executable code.

Use 2-space indentation.

    button.clicked += () => {
      showOnlyActive = !showOnlyActive;
      ApplyFilter(modListView, showOnlyActive);
    };

Do not use 4-space indentation for lambda bodies.

* * *

## Ternary operator

A ternary operator is an expression.

When wrapped, use standard expression continuation indentation (4 spaces).

    filterButton.text = showOnlyActive
        ? $"Show all ({totalCount})"
        : $"Show active ({activeCount}/{totalCount})";

Do not use 2-space indentation.

Avoid:

    filterButton.text = showOnlyActive
      ? $"Show all ({totalCount})"
      : $"Show active ({activeCount}/{totalCount})";

Ternary operators follow the same formatting rules as other wrapped expressions.

* * *

## Blank lines

Use blank lines between logical blocks.

    if (resetButton?.parent == null) {
      return;
    }

    var filterButton = new Button() {
        name = "ShowActiveModsButton",
    };

    resetButton.parent.Insert(resetButton.parent.IndexOf(resetButton) + 1, filterButton);

Do not add blank lines after every statement.

* * *

## XML documentation

Public types and public members should have XML documentation.

This applies to public methods, properties, constructors, nested types, and reusable public helpers. Documentation is
part of the public API contract, even when the member name looks self-explanatory.

Avoid documentation that only repeats the member name in prose. If the first draft sounds like a copy of the name, think
again about what a caller needs to know:

  * the intended use case,
  * which inputs are accepted,
  * what is escaped, encoded, parsed, cached, or persisted,
  * return values and failure modes,
  * side effects,
  * ordering, lifecycle, or threading expectations,
  * compatibility promises for saved data, scripts, public APIs, or other mods.

Example: a method named `EscapeSpecialSymbols` still needs documentation explaining which symbols are escaped and for
which target format or parser.

Private and internal members do not need XML documentation by default, but add it when a helper defines a subtle
contract that future maintainers are likely to misuse.

For a DTO or container whose values need separate semantic explanation, prefer explicit read-only properties and place
the documentation on those properties. Do not force important member documentation onto positional-constructor
parameters when that makes the data model harder to read.

Keep a positional record or record struct compact when the container is obvious and its parameter names fully explain
the values. Do not expand a simple container into boilerplate properties merely to follow the more explicit form used
by a different, less obvious DTO.

* * *

## General philosophy

The desired result should look like compact Java-style C#.

Characteristics:

  * 2-space indentation for executable code.
  * Same-line opening braces.
  * Minimal access modifiers.
  * `var` when obvious.
  * 120-character line limit.
  * No unnecessary wrapping.
  * Preserve method signatures when wrapping calls.
  * Wrap only argument lists when possible.
  * Operators move with the following operand in wrapped expressions.
  * Preserve logical execution groups in wrapped expressions.
  * 4-space indentation for:
    * object initializers,
    * collection initializers,
    * wrapped argument lists,
    * wrapped expression continuations, including ternary operators.
  * 2-space indentation for:
    * executable code,
    * lambdas,
    * conditionals,
    * loops.

When multiple formatting choices are valid, prefer the version with fewer lines while maintaining readability.
