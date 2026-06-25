# SmartHaulers Developer Notes

These are working notes for the SmartHaulers prototype.

They are allowed to be detailed. Include class names, method names, variable names, decompiled-source findings,
rejected assumptions, performance caveats, and prototype strategies when they help future agents avoid repeating the
same research.

These notes are not a final design spec.

## Prototype Direction

Do not reduce SmartHaulers to "fix one hauler selection algorithm". Treat it as a prototype for a dispatch model layered
over vanilla hauling and transport.

This direction is for exploration, not a final design spec and not a list of prohibitions. The goal is to help agents
keep the research broad while sharing the same working vocabulary.

The current direction is: observe first, explain decisions second, and intervene carefully only after the transport
economy is visible enough to compare vanilla behavior with SmartHaulers-owned choices.

Working orientation:

- Observability before control. First expose agents, queued or estimated orders, active deliveries, implicit transport,
  reservations, source and target, goods and amounts, distances, agent status, needs, and current task. If the mod
  cannot explain why an agent is carrying something, it is too early to change that decision.
- Keep a vanilla-compatible mental model. SmartHaulers may intentionally differ from vanilla behavior, but every
  difference should be explicit and treated as mod-owned logic, not as a Timberborn contract.
- Treat orders as first-class objects. Aim to model source, target, good, amount or range, weight or urgency, phase,
  requester, reservation, and assigned agent while preserving where the order came from: explicit haul-provider request,
  implicit job or workplace transport, active reservation, or carry task.
- Treat agents as transport-capable resources, not just haulers. Useful states include idle, wandering, workplace idle,
  working in a building, returning to workplace, and actively carrying. Useful data includes position, activity,
  interruptibility, speed, capacity, critical needs, current reservation or carry task, workplace, and district.
- Prefer explainable optimization. Future intervention should be able to explain why agent X was chosen, which distance,
  speed, capacity, ETA, or need-risk factors mattered, and why the next candidate was worse.
- Use minimal intervention first: diagnostics only, then passive "what the dispatcher would choose" comparison, then
  narrow soft intervention, then broader dispatch policy.
- Keep needs and cancellation policies as a later phase. The final direction may include detecting harmful delivery
  states such as critical food, water, fuel, or rest needs, low speed, exploding ETA, or a task locked on the wrong
  agent. Possible policies include continue, cancel before pickup, drop or cancel after pickup, or prevent immediate
  reassignment to an agent that must satisfy needs. Do not make this the first optimization.
- Be performance-aware without optimizing too early. Prefer event-driven or cadence-based models, dirty queues,
  snapshots, cached distances, and batching where they keep the prototype clear. Polling or tick-based collection is
  acceptable while the required data, change frequency, and bottlenecks are still being discovered. Early optimization
  is fine only when it is almost free in complexity and improves clarity, such as avoiding frame-by-frame UI rebuilds or
  repeated pathfinding calls.
- Keep the parallel-computation path open when it costs little: separate game-object data collection from pure ranking
  or ETA computation, prefer simple snapshot DTOs with explicit inputs and outputs, and avoid pulling Unity objects into
  hot compute loops unless needed.

## Confirmed Vanilla Hauling Model

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

## Inventory Lookup And Distance

For common flows such as `TryCarryFromAnyInventory` and `TryCarryToAnyInventory`, `CarrierInventoryFinder` chooses the
source or target inventory by distance between inventories or accessibles. It does not choose based on the current
agent's distance to the start of the order.

`CarrierInventoryFinder` delegates inventory lookup to `DistrictInventoryPicker`: carry-from flows use
`ClosestInventoryWithStock(...)`, and carry-to flows use `ClosestInventoryWithCapacity(...)`.

Vanilla inventory lookup is indexed by district, good, and unreserved stock or capacity. `DistrictInventoryRegistry`
and `InventoryRegistry` expose active inventories with stock or capacity for a `goodId`, backed by per-good
`HashSet<Inventory>` collections that are maintained from inventory changes.

After that filtering, `DistrictInventoryPicker` still iterates the matching `ReadOnlyHashSet<Inventory>` candidates and
chooses the minimum road-path distance. No inventory spatial nearest index, such as a kd-tree or grid over inventories,
was found in the decompiled game sources. Treat the candidate scan as roughly O(k), where k is the number of active
public inventories in the district that match the good and stock/capacity condition.

Path distance goes through `Accessible.FindRoadPath(...)`, `NavigationService.FindRoadPath(...)`, and
`PathfindingService.FindRoadPathCached(...)`. The flow-field cache can reduce repeated pathfinding cost from the same
start node, but it does not remove the candidate iteration.

If SmartHaulers evaluates many possible orders or distances, account for this vanilla shape. The game already has a
district plus good plus stock/capacity index, but not a spatial nearest-inventory lookup on top of it. Prefer
SmartHaulers-owned caching, batching, or cadence limits over repeatedly calling the picker in frame-rate diagnostics.

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

## Road-Distance Ranking Strategy

For road-distance ranking, SmartHaulers may compute `building or inventory access -> agent position` distance instead
of `agent -> building or inventory` distance. This can use the same distance value for ranking while aligning the
origin with vanilla flow-field caching.

Finished buildings commonly have cached road flow fields through `BuildingCachingFlowField`, which calls
`INavigationCachingService.StartCachingRoadFlowField(accessCoordinates)` while the building is finished and stops that
cache when the building leaves the finished state.

Use public navigation APIs. `INavigationService.FindRoadPath(start, end, out distance)` uses cached pathfinding, and
`INavigationCachingService.StartCachingRoadFlowField(Vector3Int coordinates)` /
`StopCachingRoadFlowField(...)` can explicitly hold a cache for a SmartHaulers-owned origin. Do not touch internal
`PathfindingService`, `RoadFlowFieldCache`, or `FlowFieldCache` directly.

This inversion is suitable for road-distance estimates and ranking. `RoadNavMeshGraph` uses bidirectional road
connections with equal cost, and the vanilla pathfinding code also has reversed cached path request logic. Use the
result as a ranking estimate, not as the final physical path an agent must follow.

For multi-access origins, evaluate each access point explicitly and use the minimum distance. Avoid
`Accessible.FindRoadPath(Vector3)` for this because it can require a single access point.

If SmartHaulers starts its own road-flow-field cache for large batches, balance every
`StartCachingRoadFlowField(...)` with the matching `StopCachingRoadFlowField(...)`. The vanilla flow-field cache is
reference-counted: an extra stop can throw, and a missing stop leaves the cache alive.

## Navigation Caveats

Avoid calling `Accessible.FindRoadPath(Vector3)` on an accessible that may have multiple access points. Internally it
uses `UnblockedSingleAccess`, which can throw when more than one access point exists.

Use a multi-access-aware API when available. In diagnostics, prefer returning unknown, NaN, or no path over crashing on
a multi-access building.

## Diagnostics Cadence

`UpdateSingleton` and UI frame updates may run every frame, including while the game is paused.

Avoid rebuilding expensive transport snapshots every frame. Prefer game-tick cadence for regular diagnostics refreshes,
plus one-shot refreshes when diagnostics are enabled, the view mode changes, or the user explicitly requests a snapshot.

## Open Questions

Add open questions here when a behavior is not yet proven from decompiled sources, real-game testing, or prototype
evidence.

## Rejected Assumptions

- Vanilla assigns the nearest hauler to a transport request.
- Every transport-like activity is represented as an `IHaulBehaviorProvider` request.
- Supporting every `IJobBehavior` means supporting every transport order.
