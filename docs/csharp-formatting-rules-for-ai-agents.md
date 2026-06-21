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

* * *

## Access modifiers

Do not write access modifiers unless they are required.

Prefer:

    static class Helper {
      static void DoWork() {
      }
    }

Avoid:

    internal static class Helper {
      private static void DoWork() {
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

Do not force one argument per line when several arguments fit comfortably within the 120-character limit.

This rule applies to method calls, method declarations, constructors, and primary constructors.

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
