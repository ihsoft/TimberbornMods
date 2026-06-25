# SmartHaulers Agent Notes

These notes apply only when working inside the SmartHaulers mod.

## Prototype Context

SmartHaulers is currently a prototype.

Keep this file focused on local rules, confirmed vanilla behavior, and links to deeper working notes. It is not a final
design spec and should not narrow the search for a working solution.

For detailed investigation notes, design orientation, explored game classes, prototype strategies, caveats, and open
questions, read:

- `SmartHaulers/docs/developer-notes.md`

Agents may prototype behavior that differs from the base game. When doing so, make the difference explicit: treat it as
SmartHaulers-owned logic, not as existing Timberborn behavior.

Some notes describe promising SmartHaulers prototype strategies, not vanilla contracts. Keep that distinction explicit
when using or changing them.

## Confirmed Vanilla Behavior

- Timberborn does not centrally assign hauling work to the nearest agent.
- Hauling workers choose work during their own behavior tick.
- Candidate haul behaviors are ordered by `WeightedBehavior.Weight`, not by agent-to-source distance.
- Hauling assignee selection should be treated as nondeterministic for mod-design purposes.
- Reservations through `GoodReserver` are the lock point for stock or capacity.
- `CarrierInventoryFinder` chooses source or target inventories by inventory/access distance, not by current agent to
  order-start distance.
- Vanilla inventory lookup is indexed by district, good, and unreserved stock/capacity, then scans matching inventory
  candidates for minimum road-path distance.
- Not every transport-like activity is an `IHaulBehaviorProvider` request.
- `IJobBehavior` is broader than transport.
- `Accessible.FindRoadPath(Vector3)` is unsafe for accessibles that may have multiple access points.

## Prototype Guardrails

- Prefer observability before control: diagnostics first, comparison second, intervention later.
- Keep explicit vanilla-vs-SmartHaulers distinctions when implementing nearest-agent, dispatcher, or ranking logic.
- Preserve explicit vs implicit transport origin when that distinction matters, unless a prototype deliberately
  collapses them.
- For road-distance ranking, `building/inventory access -> agent position` can be a useful SmartHaulers-owned strategy,
  but treat it as a ranking estimate, not as a final movement path.
- Avoid rebuilding expensive diagnostics snapshots every frame; prefer game-tick cadence plus one-shot refreshes for UI
  toggles or explicit snapshot requests.
