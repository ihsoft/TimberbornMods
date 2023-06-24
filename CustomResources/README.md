# Custom resources mod

This mod allows loading mod's resources in some stock game functions.

## Background

Some very useful stock game classes ask to provide a "resource name". Then, the "name" is get
resolved to a path to the Unity resource. When it happens, the path points to the stock game
folder(s), not the mod's assets! In such cases, the mod developers choose to patch the game
methods via Harmony. However, Timberborn plug-in system is based on `HarmonyX`, which is not
exactly compatible with the original `Harmony`.

TL;DR; It's not possible in HarmonyX to intercept a call to any method.

## Why cannot I intercept a call?!

It's due to [this](https://github.com/BepInEx/HarmonyX/wiki/Difference-between-Harmony-and-HarmonyX#all-prefix-patchers-are-always-run-even-if-original-method-is-skipped).
You can like it or not, but the matter of truth, all the prefixes are always get called on
the patched method. Unless _all the patches_ respect the `__runOriginal` approach, it's not
possible to reliably patch the method call result. If you decided to do it, and another mod also
patched the same method, then the game will get broken _for sure_.

You may not observe any issues during the development, but it's only because your game
doesn't have the breaking mod. In the field, there will be some.

## What should I not do?

In general, don't patch methods to change the output. If you need to observe the call, it's
fine. If you need to change the output, STOP! `HarmonyX` has made this way
<i>intentionally</i> hard. You may think your patch changes the output, but in fact you're
just planting a bomb that will detonate when another mod decides to do the same.

## What should I do?

Use <i>this</i> mod! It patches the common cases in a way that keeps compatibility across all
the mods. If you need something that is not yet supported, just ping the author via the issues
page. Or suggest a PR if you know how to do what you need.
