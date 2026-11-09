# Automation Hook Integration Instructions

## Scope And Reference

Read these instructions when authoring or reviewing `AutomationHooks*.json` integrations or changing the hooks
implementation in this directory. Unrelated mod work does not need this integration guide.

Read the adjacent [HookConfiguration.md](HookConfiguration.md) before editing a declaration. It owns the complete JSON
contract, packaging rules, observable lifecycle, limitations, and examples; do not invent fields or reconstruct
the contract from an older source API. Use the reference matching the intended Automation implementation/version.
The contract in this checkout is not proof that a published Automation version supports it.

For agents working in a third-party repository, this is integration guidance, not a replacement for that repository's
instructions, ownership, validation, or release permissions. The external-author sections below and the adjacent
`HookConfiguration.md` provide the integration instructions; reading TimberbornMods' internal agent rules is not
required. Work in your own mod repository and inspect its source or DLL metadata for the target API. No web lookup is
required to learn the JSON format. If the matching reference or target API evidence is unavailable, report that gap
instead of guessing. The final Automation-maintainer section does not apply to external configuration authors.

## External Authors: Authoring Workflow

1. Establish the intended game, Automation, and provider versions. Inspect provider source or DLL metadata for exact
   assembly/type names, member signatures, and singleton/component/static ownership. Display names are not CLR identity.
2. Use the reference to select the source scope, read projection, watched setters, actions, and value metadata. For each
   changing signal, establish which setter is called when its value may have changed. A readable property alone does
   not establish notification support; do not substitute `Watch: []` for a missing hook or assume a polling fallback.
3. Keep read projections cheap, side-effect free, and safe at deferred sampling and initial subscription. Respect the
   documented game-thread, deferred-notification, and source-lifecycle boundaries. If the provider cannot meet them,
   report the integration limitation rather than silently adding a new hooks mechanism.
4. Preserve provider/capability IDs and stored option values used by existing rules. Supply localization keys and
   translations through the owning mod's normal localization resources. Check units, numeric precision, argument order,
   and option lifetime against the reference, not merely JSON syntax.
5. Package the JSON alongside the manifest in the selected mod version directory, following the reference's discovery
   rules. Do not add Automation DLLs, helper source, registration calls, or a required Automation dependency merely to
   supply this optional integration. Verify the final package contains the intended config and localization resources.

## External Authors: Validation And Handoff

Validate syntax and declarations separately from runtime behavior. A parse, compile, or successful registration does
not prove setter interception. Check the packaged integration in the intended game with the matching Automation and
provider versions. Follow your own repository's testing, user-approval, commit, and release requirements.

Use the reference's validation checklist for the applicable scopes: discovery/logs, localized editor controls, real
and same-value writes, action-driven changes, save/reload, building isolation, and optional-mod absence. Report what was
actually checked and what remains unverified; do not label illustrative examples as tested integrations.

## Automation Maintainers Only

This section applies when maintaining the hooks implementation inside TimberbornMods, not when supplying JSON from
another mod repository. Read the repository root `AGENTS.md`, `Automation/AGENTS.md`, and their applicable task routes.
Read [HookImplementation.md](HookImplementation.md) for class ownership, DI/patch lifecycle and deferred processing.
External JSON authors do not need that document. Keep schema and observable behavior in `HookConfiguration.md`, and
implementation mechanisms in `HookImplementation.md`; do not make the external contract depend on reading both.
In TimberbornMods, user real-game confirmation precedes new regression tests and commit unless explicitly waived.

When changing the hooks implementation, keep the technical reference and focused regression coverage aligned with the
validated contract. Follow the parent Automation instructions and repository C# finalization rules. A configuration-only
task does not authorize changes to the hooks engine, published API, Wiki, or release state.
