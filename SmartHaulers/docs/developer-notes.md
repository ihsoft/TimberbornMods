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

## Carry Capacity Semantics

`GoodCarrier.LiftingCapacity` is weight capacity in kilograms, not a count of item units.

The number of carriable units depends on `GoodSpec.Weight`. Vanilla `GoodCarrierFragment` displays carried weight as
`goodAmount.Amount * good.Weight` next to `LiftingCapacity`.

Vanilla `CarryAmountCalculator.AmountToCarry(...)` effectively computes a maximum unit count from weight first:

```text
maxUnits = Math.Max(liftingCapacity / good.Weight, 1)
```

It then limits that count by available stock, request amount, and target capacity.

For SmartHaulers scoring and diagnostics, do not compare `GoodAmount.Amount` directly with `LiftingCapacity`. Convert
through the good weight or use the same carry-amount semantics as vanilla.

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

## Possible Order Planning

The current SmartHaulers possible-order planner uses vanilla `DistrictInventoryPicker.ClosestInventoryWithStock(...)`
and `ClosestInventoryWithCapacity(...)` to estimate a request.

These APIs return one closest matching inventory. They do not expose an ordered iterator or list of next-best
candidates.

As a current prototype limitation, each candidate is one estimated source-to-target segment. `FillInput` can create
multiple alternative source candidates for one good across source inventories, but this is not compound planning. For
example, if a target can accept `5x Log`, the closest source has `3x Log`, and another source has `2x Log`, the current
planner can expose alternatives, but it does not create a `3 + 2` compound coverage plan in one pass.

Repeating the vanilla picker while filtering out already chosen inventories can emulate "next inventory", but each
repeat is another candidate scan and path-distance pass. Treat that as a prototype shortcut, not as a scalable planning
model.

A future SmartHaulers-owned compound or batch planner likely needs to enumerate candidate inventories from the
district/good registry, rank them, and apply virtual stock/capacity subtraction across multiple planned segments.

The same candidate expansion problem exists on the target side. Take-away or product-export flows may need to move more
goods than the closest accepting storage can hold, so future planning may need multiple target candidates or compound
target coverage for `EmptyOutput`, `RemoveUnwantedStock`, `EmptyInventories`, and possibly `SupplyGood` or `ObtainGood`
flows where stock or capacity limits create alternatives.

Treat planned orders as snapshot-bound candidates. They are execution options computed from one view of district stock,
capacity, reservations, and active deliveries, not a durable independent queue. Candidate conflicts can span multiple
vanilla requests: two different factories may both produce candidates that use the same source inventory.

Without virtual subtraction, the safe prototype strategy is to refresh the snapshot, choose one best dispatchable
candidate, perform the real reservation or assignment, then refresh or rebuild affected candidates before choosing the
next one. Rebuilding the whole district after an assignment is acceptable while the prototype is still simple.

Virtual subtraction is only needed when SmartHaulers wants to batch-plan several assignments from one snapshot before
making real reservations. Until then, prefer fresh game state over pretending the old candidate set is still current.

For per-good `FillInput` planning, vanilla `weight = 1 - GetInputFillPercentage(inputInventory)` is a weak heuristic
for multi-ingredient recipes. It can hide a critically missing ingredient when another ingredient fills much of the
shared input inventory. SmartHaulers should treat per-good demand and urgency as an intended improvement direction, not
only overall input inventory fill percentage. Example: a recipe needs logs and water; logs are full, water is empty.
The shared input inventory may look partly full, but water delivery should still be urgent.

Current prototype working assumption: for ordinary buildings, treat there as being at most one active meaningful
inventory at a time. A construction inventory is active while a building is under construction; after construction it is
deactivated and no longer relevant. If a finished building has input or output storage, that production inventory is the
active one. For current planning work, assume a building has one active inventory or none, not multiple simultaneous
input inventories. Revisit this if decompiled sources or real-game evidence show an important exception.

Current take-away per-good planning uses per-good source fill ratio as weight/readiness input when possible:

```text
fillRatio = AmountInStock(goodId) / LimitedAmount(goodId)
```

If `LimitedAmount(goodId) <= 0` and stock is present, treat the good as full/urgent with `fillRatio = 1`. This matters
for disallowed or unwanted goods: otherwise they can remain forever `Deferred` even though any stock should be removed.

## Time-Based Readiness

`TransportOrderReadinessClassifier` currently prefers time-to-critical estimates when
`TransportOrderCriticalTimeEstimator.TryGetHoursUntilCritical(...)` can provide them. Fixed per-good fill thresholds are
fallback behavior for order types where a time estimate is not implemented yet.

Current time estimate coverage:

- `FillInput` into a manufactory can estimate time until a required ingredient or fuel becomes critical.
- `FillInput` into a `GoodConsumingBuilding` can estimate time until the building exhausts a supplied good.
- `EmptyOutput` from a manufactory can estimate time until product output storage blocks production.

Manufactory input estimates use `CurrentRecipe`. Ingredient availability is counted in future recipe cycles from
unreserved stock, and `_ingredientsConsumed` plus remaining cycle time determine whether the current cycle has already
consumed its ingredients. Fuel estimates use stored fuel, `FuelRemaining`, and `CyclesFuelLasts` to estimate how many
future cycles can still run. Output estimates count unreserved output capacity for recipe products and include remaining
cycle time when the current cycle can still complete.

`GoodConsumingBuilding` estimates use the per-good `GoodPerHour` rate and combine inventory stock with the building's
internal supply for that good.

Current limitations:

- production efficiency, power state, paused/disabled state, and other limiters are not fully folded into readiness;
- delivery ETA is not yet compared against time-to-critical when deciding whether an order is dispatchable;
- the current urgency windows are prototype constants, not final dispatch policy.

Keep the distinction clear: time-to-critical readiness is SmartHaulers-owned prototype logic, not vanilla behavior.

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

## Haul Request Weight Semantics

These are baseline vanilla weight sources for supported haul-provider requests in the current SmartHaulers model.
SmartHaulers may intentionally replace or reinterpret these weights later, but future scoring work should know what the
vanilla values mean.

- `RemoveUnwantedStock`: vanilla constant `0.5` from `UnwantedStockHaulBehaviorProvider`; current SmartHaulers uses it
  as-is.
- `EmptyInventories`: vanilla constant `0.51` from `EmptiableHaulBehaviorProvider`; current SmartHaulers uses it as-is.
- `FillInput`: vanilla fill-based weight, but the SmartHaulers prototype now expands it to per-good weight
  `1 - AmountInStock(goodId) / LimitedAmount(goodId)` for each input good.
- `BringNutrient`: vanilla `1 - InventoryFillCalculator.GetInputFillPercentage(inventory)`; current SmartHaulers uses
  it as-is.
- `ObtainGood`: vanilla `1 - GetInputFillPercentage(stockpileInventory)`. Current SmartHaulers keeps the vanilla
  weight as input or fallback, but uses soft source tiers: `Supply` stockpiles, then ordinary non-obtaining stockpiles,
  then a narrow producing-building fallback currently limited to `Manufactory` outputs.
- `SupplyGood`: vanilla `GetInputFillPercentage(stockpileInventory)`. Current SmartHaulers keeps the vanilla weight as
  input or fallback, but uses soft target tiers: `Obtain` stockpiles, then ordinary stockpiles, while excluding
  stockpiles already in `Supply` mode.
- `EmptyOutput`: vanilla output fill percentage. Manufactories use `GetOutputFillPercentage(outputInventory)`;
  simple-output buildings use `GetInStockOutputFillPercentage(inventory)`. A District Center has
  `SimpleOutputInventorySpec` and can naturally produce `EmptyOutput` with weight `1` when an in-stock good reaches its
  per-good limit; this is computed, not a special constant.

Vanilla `HaulCandidate.PrioritizeAndValidate` adds `PriorityFactor = 0.5` when `HaulPrioritizable.Prioritized` is true
and `weight >= 0.5`. This means baseline weights such as `0.5` and `0.51` can become `1.0` and `1.01` after priority is
applied.

## Covered Requests

A `Covered` order should not be treated as a concrete single source-to-target route.

`Covered` means the request is currently covered by existing active reservations or deliveries. That coverage may come
from multiple agents, multiple source inventories, multiple target inventories, or a mix of these.

For diagnostics and future decision logic, active agent delivery rows are the reliable place to inspect exact
source/target pairs. A `Covered` request may keep cargo, request, and weight information, but any source-to-target route
shown on it is at best partial and can be misleading. Do not use it as a dispatcher route contract.

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

For SmartHaulers diagnostics and planning over arbitrary source-target pairs, avoid direct
`Accessible.FindRoadPath(...)` and other cached road-path calls. They can throw when road-flow cache data is missing or
stale, and some overloads assume a single access point. Use a SmartHaulers-owned helper such as
`TransportPathDistance.TryFindRoadPath(...)` that explicitly iterates access points, uses `FindPathUnlimitedRange(...)`,
and returns no-path or unknown instead of crashing diagnostics.

For multi-access origins, evaluate each access point explicitly and use the minimum distance.

If SmartHaulers starts its own road-flow-field cache for large batches, balance every
`StartCachingRoadFlowField(...)` with the matching `StopCachingRoadFlowField(...)`. The vanilla flow-field cache is
reference-counted: an extra stop can throw, and a missing stop leaves the cache alive.

## Navigation Caveats

Avoid calling `Accessible.FindRoadPath(Vector3)` on an accessible that may have multiple access points. Internally it
uses `UnblockedSingleAccess`, which can throw when more than one access point exists.

Avoid calling `Accessible.FindRoadPath(Accessible)` for arbitrary diagnostic/planning ranking too. It can rely on cached
road flow fields and throw `There's no cached flow field` when the cache is not ready, stale, or missing.

Use a multi-access-aware API when available. In diagnostics, prefer returning unknown, NaN, or no path over crashing on
a multi-access building.

Cached road flow-field data may be missing after save load or before vanilla has warmed the relevant cache. Diagnostics
should not crash or assume the cache exists. For endpoint restoration, prefer a safe best-effort registry scan and
`FindPathUnlimitedRange` over vanilla `DistrictInventoryPicker` paths that can throw through `UnblockedSingleAccess`.
An unknown endpoint is better than crashing the diagnostics panel.

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
