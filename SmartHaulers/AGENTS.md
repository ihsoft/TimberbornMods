# SmartHaulers Agent Notes

These notes apply only when working inside the SmartHaulers mod.

## Purpose

SmartHaulers is currently a prototype.

These notes are not a final design spec and should not narrow the search for a working solution. They record confirmed
facts about Timberborn's current hauling, transport, navigation, and diagnostics behavior so agents do not rediscover
the same game behavior or accidentally treat disproven assumptions as vanilla logic.

Agents may prototype behavior that differs from the base game. When doing so, make the difference explicit: treat it as
SmartHaulers-owned logic, not as existing Timberborn behavior.

## Timberborn Hauling Model

Timberborn does not centrally assign hauling work to the nearest agent.

A hauling center worker chooses work during its own behavior tick. `HaulWorkplaceBehavior.Decide()` gets candidates
from `HaulingCenter.GetWorkplaceBehaviorsOrdered()` and district haul candidates. Candidates are ordered by
`WeightedBehavior.Weight`, not by distance from the current beaver or bot to the source or requester.

For mod-design purposes, treat hauling assignee selection as nondeterministic. The agent that ticks first, finds a
suitable candidate, and successfully reserves stock or capacity gets the work. Do not rely on entity or tick execution
order as a stable game contract.

Reservations are the lock point. After `GoodReserver` reserves goods or capacity, other agents see reduced unreserved
stock or capacity and should not take the same volume. After accepting a carry task, an agent usually keeps it until
completion or explicit cancellation by game behavior or UI.

## Inventory And Distance Semantics

For common flows such as `TryCarryFromAnyInventory` and `TryCarryToAnyInventory`, `CarrierInventoryFinder` chooses the
source or target inventory by distance between inventories or accessibles. It does not choose based on the current
agent's distance to the start of the order.

Do not assume that "nearest agent to point A" is existing game behavior. If SmartHaulers prototypes or implements that
model, treat it as mod-owned logic and document the difference from the base game.

## Transport Order Categories

Do not assume that every transport-like activity is an `IHaulBehaviorProvider` request.

Explicit haul-provider requests include behaviors such as `FillInput`, `EmptyOutput`, `EmptyInventories`,
`RemoveUnwantedStock`, `SupplyGood`, `ObtainGood`, `BringNutrient`, and similar building or storage
`WeightedBehavior` requests.

Some transport is implicit inside job or workplace behavior, such as construction material delivery through
`ConstructionJob` or `GoodStackRetrieverBehavior`, harvest, gather, scavenge, and lumberjack flows. When diagnostics or
dispatching logic needs transport coverage, keep explicit haul-provider requests and implicit job or workplace
transport distinguishable unless a prototype deliberately collapses them.

`IJobBehavior` is too broad to mean transport. Its implementors include planting, demolishing, build execution, labor,
and other non-transport behavior. Do not assume that validating or supporting "all `IJobBehavior`" means validating or
supporting all transport orders.

## Navigation Diagnostics

Avoid calling `Accessible.FindRoadPath(Vector3)` on an accessible that may have multiple access points. Internally it
uses `UnblockedSingleAccess`, which can throw when more than one access point exists.

Use a multi-access-aware API when available. In diagnostics, prefer returning unknown, NaN, or no path over crashing on
a multi-access building.

## Diagnostics Cadence

`UpdateSingleton` and UI frame updates may run every frame, including while the game is paused.

Avoid rebuilding expensive transport snapshots every frame. Prefer game-tick cadence for regular diagnostics refreshes,
plus one-shot refreshes when diagnostics are enabled, the view mode changes, or the user explicitly requests a snapshot.
