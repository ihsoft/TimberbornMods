# SmartHaulers Design Draft

This is an early design draft for the SmartHaulers prototype.

It is not a finished specification. It captures the SmartHaulers-owned model that is already partially implemented or
strong enough to guide the next implementation steps. Confirmed vanilla behavior and investigation details remain in
`developer-notes.md`.

## Research Goal

SmartHaulers is a prototype for testing whether Timberborn hauling can be improved by building a district-level,
evidence-based view of transport work and using it to discover where meaningful intervention points actually exist.

The project challenges the assumption that vanilla hauling choices stay good enough as a colony grows, especially when
distance, reservations, critical needs, stockpile policies, and resource-output flows interact.

SmartHaulers starts with diagnostics because intervention without a trusted model is likely to make the economy worse.
Before broad behavior changes, diagnostics should show what vanilla already did, what SmartHaulers would have chosen,
why the two differ, and whether the difference repeats in controlled saves.

Prototype interventions are allowed, but they should stay narrow and reversible until the observed model is proven.

## Current Design Goal

SmartHaulers is moving toward a district-level dispatch model layered over Timberborn hauling.

The goal is not only to make haulers pick closer jobs. The broader goal is to make transport work visible, explainable,
and eventually controllable:

- expose transport-capable agents;
- expose queued, estimated, covered, and active transport orders;
- estimate what a dispatcher would do before changing vanilla behavior;
- compare vanilla choices with SmartHaulers choices;
- intervene later only when the dispatcher model is clear enough to explain its decisions.

## Dispatcher Scope

The current dispatcher is attached to `DistrictCenter`.

This matches Timberborn's district-local hauling model: workers, inventories, haul candidates, and path connectivity
are evaluated inside a district. Cross-district transport is out of scope for the current prototype.

The dispatcher currently observes and estimates. It does not reserve stock, assign workers, cancel work, or override
vanilla behavior.

## Transport Agents

A transport agent is any enabled district worker that can carry goods and reserve stock or capacity.

The current snapshot tracks:

- worker identity and display name;
- grid and world position;
- walking speed;
- carrying capacity in kilograms;
- coarse state;
- current activity;
- active transport order, when one can be reconstructed.

Current candidate states for passive dispatcher decisions:

- `Available`;
- `IdleWandering`;
- `WorkplaceIdle`.

These are intentionally conservative. `Working`, `Transporting`, and `SatisfyingNeed` are observed but not selected as
new-order candidates by the passive evaluator.

Agents with `WorkRefuser.RefusesWork` are visible in diagnostics but are not eligible for passive scoring. This covers
workers and bots that the game currently prevents from working, such as contaminated, injured, or critical-need agents
whose need has `NeedPreventingWorkSpec`.

`WorkplaceIdle` is too broad for passive scoring by itself. The current SmartHaulers-owned scoring model classifies the
workplace role:

- transport workplace idle: hauling-center style workers; usable helper, small penalty;
- builder workplace idle: district-center and builder-workplace workers; avoid unless useful or important; builders are
  idle now but expected to build;
- production workplace idle: manufactory or factory worker; avoid more strongly because production may resume as soon
  as inputs, power, or another blocker becomes available;
- unknown workplace idle: worst fallback because the interrupted work is unknown.

District Center workers are builders in the current model, not transport workplace workers. Builder workplace idle keeps
a large penalty because builders are expected to build.

A future SmartHaulers-owned policy may let the user opt builders into transport help through UI on a district center,
builder workplace, worker panel, or another suitable control. This is a possible micromanagement feature for early-game
pressure, not current behavior and not vanilla behavior.

## Transport Orders

SmartHaulers treats transport orders as first-class objects. An order has:

- phase;
- origin;
- source and target inventory when known;
- cargo;
- weight;
- assignment;
- route and remaining distance when known;
- optional passive dispatcher decision.

### Order Phase Reference

The phase describes the order's current planning or observation state. It is separate from origin ownership labels such
as `GAME`, `IDEA`, and `IDEA/build`.

Planned phases are snapshot-bound candidates, not a durable independent queue. They can become stale whenever stock,
capacity, reservations, active deliveries, building state, or the district snapshot changes.

`GAME` phases observe work already assigned or reserved by Timberborn. SmartHaulers does not own those assignments.

`Queued`

- Meaning: a vanilla request exists, but SmartHaulers has not found a concrete executable route and cargo.
- Owner label: usually `IDEA`, because it comes from SmartHaulers request planning, but it is not executable yet.
- Decision: produced when the vanilla request is visible but route/cargo planning cannot form a concrete candidate.
- Route data: no useful route contract; source, target, or cargo may be missing or diagnostic-only, and cargo can be
  `0x`.
- Scoring: not evaluated by passive decision scoring.
- Staleness: rebuild the snapshot; a later inventory, capacity, reservation, or path change may turn it into a concrete
  planned candidate.

`Estimated`

- Meaning: SmartHaulers found a concrete source, target, and cargo candidate that could be executed if started now, but
  readiness did not make it urgent enough for scoring.
- Owner label: `IDEA`.
- Decision: produced by possible-order planning followed by readiness classification. With a time estimate, current
  `4h < t <= 12h` stays `Estimated`.
- Route data: source, target, and cargo describe a snapshot-bound candidate route, not a reservation.
- Scoring: not evaluated by passive decision scoring.
- Staleness: any stock, capacity, reservation, active-delivery, or readiness-input change can invalidate it.

`Deferred`

- Meaning: SmartHaulers found a concrete candidate, but it is intentionally not urgent.
- Owner label: `IDEA`.
- Decision: readiness classification sets it when a time estimate is `t > 12h` or when fallback fill thresholds say the
  order is not dispatchable.
- Route data: source, target, and cargo describe a snapshot-bound candidate route, not a reservation.
- Scoring: not evaluated by passive decision scoring.
- Staleness: same as `Estimated`; it may become `Estimated` or `Dispatchable` after the next snapshot/readiness pass.

`Dispatchable`

- Meaning: SmartHaulers found a concrete candidate that is eligible for passive decision scoring.
- Owner label: `IDEA`, or `IDEA/build` for planned construction candidates.
- Decision: readiness classification sets it when a time estimate is `t <= 4h`, fallback fill thresholds pass, or the
  candidate is construction work, which is currently always dispatchable.
- Route data: source, target, and cargo describe a snapshot-bound candidate route, not a reservation.
- Scoring: this is the only planned-order phase evaluated by passive decision scoring.
- Staleness: scoring is valid only for the snapshot that produced the candidate.

`Covered`

- Meaning: the request appears covered by existing reservations or deliveries.
- Owner label: `IDEA`, because it is SmartHaulers' interpretation of coverage for a request, not a new game assignment.
- Decision: produced when existing active work/reservations appear to cover the request.
- Route data: not a single route contract. Coverage may come from multiple agents, sources, targets, or deliveries.
  Active `GAME` rows are the reliable source for concrete route details.
- Scoring: not evaluated by passive decision scoring.
- Staleness: coverage must be recomputed from active reservations and deliveries in each snapshot.

`PickingUp`

- Meaning: an agent has an active reservation/order and is moving toward the source.
- Owner label: `GAME`.
- Decision: reconstructed from the agent's active Timberborn work/reservation state.
- Route data: diagnostic route and remaining-distance data describe game-assigned work already in progress.
- Scoring: not evaluated as a new candidate; SmartHaulers observes it.
- Staleness: changes when Timberborn advances, cancels, completes, or replaces the agent's work.

`Delivering`

- Meaning: an agent has picked up cargo and is carrying it toward the target.
- Owner label: `GAME`.
- Decision: reconstructed from the agent's active carry/delivery state.
- Route data: diagnostic route and remaining-distance data describe game-assigned work already in progress.
- Scoring: not evaluated as a new candidate; SmartHaulers observes it.
- Staleness: changes when Timberborn advances, cancels, completes, or replaces the agent's work.

Readiness changes phase only. It does not mutate `Weight`. `Weight` is stored in `TransportOrderOrigin.Weight` when the
order is created and currently remains diagnostic/order-priority data.

In the phase thresholds, `t` means the current `CriticalTimeInHours` estimate: how many in-game hours remain before the
target building runs out of the delivered good or the source building becomes blocked by the taken-away good. It is a
prototype time-to-critical estimate, not delivery ETA.

Current readiness constants:

- `UrgentDeliveryThresholdInHours = 4`.
- `RelaxedDeliveryThresholdMultiplier = 3`, so the relaxed threshold is `12h`.
- Time-based readiness is implemented for `FillInput` into manufactories, fuel-consuming production, and
  `GoodConsumingBuilding` supply, and for `EmptyOutput` from manufactories.
- Fallback fill thresholds still apply where time-to-critical is unavailable: bring/fill behaviors become
  `Dispatchable` at target fill ratio `<= 0.5`; take-away behaviors become `Dispatchable` at source fill ratio
  `>= 0.5`; construction is always `Dispatchable`.

## Possible Order Planning

The current possible-order planner expands vanilla haul-provider requests into SmartHaulers order snapshots.

Planned orders are snapshot-bound candidates, not an independent queue of real orders. `Estimated`, `Deferred`, and
`Dispatchable` describe execution options that are valid for the stock, capacity, reservations, and active deliveries
seen in the snapshot that produced them.

Some supported behaviors still produce at most one SmartHaulers order per vanilla request:

- `BringNutrient`;
- `ObtainGood`;
- `SupplyGood`.

Current SmartHaulers `ObtainGood` and `SupplyGood` planning treats stockpile priority modes as soft intent for
stockpile balancing. Unlike vanilla `ObtainGoodWorkplaceBehavior`, SmartHaulers does not treat every reachable
inventory as equivalent.

`ObtainGood` source candidates are ranked by tier:

- tier 0: `Supply` stockpiles;
- tier 1: ordinary non-obtaining stockpiles;
- tier 2: producing building outputs, currently intentionally limited to `Manufactory` output inventories.

`SupplyGood` target candidates are ranked by tier:

- tier 0: `Obtain` stockpiles;
- tier 1: ordinary stockpiles.

`SupplyGood` excludes target stockpiles that are already in `Supply` mode.

Tier preference is not absolute. Candidate score adds a per-tier distance penalty:

```text
score = roadDistance + tier * StockpilePriorityTierDistancePenalty
```

The current prototype value is `StockpilePriorityTierDistancePenalty = 40`, so one priority tier is worth about 40 road
distance units. This is an empirical, tuning-sensitive prototype value: roughly a noticeable part of a working day at
base walking speed, but not a reason for arbitrary cross-map trips.

This tier model and the current `40` value are prototype hypotheses. They are implemented and compile, but have not yet
been proven by controlled in-game dispatch tests. Future tuning must verify that selected candidates match intended
Supply/Obtain behavior and do not reintroduce long cross-map hauling or resource-workplace misuse.

The producer fallback for `ObtainGood` is deliberately narrow. Resource workplaces such as scavenger flags, gatherer
flags, lumberjack flags, farmhouses, or forester outputs should not become generic stockpile balancing sources again.

Resource-output flows can expose an important vanilla-vs-planned gap. Timberborn can already assign specialized workers
to long `EmptyOutput` deliveries through vanilla reservation and nearest-capacity lookup, while SmartHaulers may have a
nearer snapshot-bound candidate that remains only an idea. Future intervention must decide whether and how to prevent
`Obtain` stockpiles from pulling resource-output workers across the map without breaking simple nearest-storage unload
behavior for resource workplaces.

SmartHaulers expands selected behaviors per good:

- one `FillInput` request may create multiple planned orders across `Estimated`, `Deferred`, `Dispatchable`, or
  `Covered` phases;
- one `EmptyOutput` request may create multiple take-away orders, one per unreserved output good found in enabled
  inventories;
- one `RemoveUnwantedStock` request may create multiple take-away orders, one per unwanted good;
- one `EmptyInventories` request may create multiple take-away orders, one per unreserved good from inventories being
  emptied;
- each input good gets its own cargo and weight;
- take-away goods get their own cargo and weight when expanded;
- goods are no longer prioritized only by vanilla `GoodAmountComparer`;
- priority is represented by each order's weight.

The current planner still uses vanilla nearest inventory lookup for individual candidates. Each candidate has one
concrete source-to-target segment. The planner does not yet build compound coverage from multiple source inventories or
batch-plan across multiple destinations.

For `FillInput`, the prototype can create multiple source candidates for one good across source inventories. These are
alternative candidates from one snapshot, not a compound plan. They do not virtually subtract stock, and they do not
mean SmartHaulers can already reserve several sources for one request in one pass.

The current `FillInput` source-candidate algorithm orders source inventories by road distance and stops once cumulative
planned amount covers the target `UnreservedCapacity(goodId)`.

The same multi-candidate idea likely matters for take-away and export flows. An output source may need to move more
goods than the closest accepting storage can hold, so multiple target inventories or alternative target candidates can
matter for `EmptyOutput`, `RemoveUnwantedStock`, `EmptyInventories`, and possibly `SupplyGood` or `ObtainGood` flows
where source stock or target capacity limits create alternatives.

The current take-away target-candidate algorithm orders target inventories by road distance and stops once cumulative
planned capacity covers the available amount or after `MaxTakeAwayTargetCandidates = 3`. The cap of `3` is a prototype
diagnostics limit chosen to keep the UI readable and should be revisited during tuning.

Candidates may compete across vanilla requests, not only inside one request. For example, two factories can both create
delivery candidates that depend on the same source stock. After SmartHaulers eventually performs a real assignment or
reservation, the old snapshot should be treated as stale because available stock or capacity may have changed. The safe
prototype sequence is: refresh a snapshot, choose one best dispatchable candidate, perform the real reservation or
assignment, then rebuild affected candidates before choosing the next one. Rebuilding the whole district is acceptable
at this stage.

Urgency does not automatically justify one-unit immediate dispatch. If a remote source currently has only one unit, is
likely to produce more soon, and a candidate agent can carry more than one unit, future dispatch policy should consider
a short batching window when the consumer's time-to-critical allows it. Otherwise SmartHaulers can reproduce vanilla
micro-delivery spam by sending one agent for every newly available unit.

Distance-sensitive batching must distinguish production that will block soon from production that is already blocked.
While production is still running, `CriticalTimeInHours` and delivery ETA describe how much batching slack exists. If
the building will block before a full batch can arrive, a smaller batch may be correct. Once production is already
blocked by missing input, `CriticalTimeInHours` no longer expresses urgency by itself; the question becomes restart
viability. If the available remote cargo is only a tiny fraction of an agent's carrying capacity and cannot restart
production or complete a meaningful cycle, waiting or batching can save hauler time without worsening production.

Current readiness classification is partly time-based. Fixed per-good inventory fill thresholds remain only as fallback
behavior for order types where SmartHaulers cannot yet estimate time-to-critical.

Current `CriticalTimeInHours` mechanics:

- Manufactory ingredient `FillInput` works only when the target inventory belongs to a `Manufactory` with
  `HasCurrentRecipe` and the cargo good is one of the current recipe ingredients. It counts full future recipe cycles
  from unreserved stock:

  ```text
  futureCycles = UnreservedAmountInStock(ingredient.Id) / ingredient.Amount
  ```

  If current-cycle ingredients were already consumed, it adds the remaining current-cycle time:

  ```text
  RemainingCycleHours = (1 - ProductionProgress) * CycleDurationInHours
  ```

  Otherwise current-cycle extra time is `0`, because the missing input matters before another cycle can start. If the
  ingredient amount is `<= 0`, the estimate is `float.MaxValue`.

- Manufactory fuel `FillInput` applies when the current recipe consumes the cargo good as fuel. It converts stored
  unreserved fuel and already loaded fuel into remaining recipe cycles:

  ```text
  storedFuelCycles = UnreservedAmountInStock(fuel) * CyclesFuelLasts
  loadedFuelCycles = FuelRemaining * CyclesFuelLasts
  remainingFuelCycles = storedFuelCycles + loadedFuelCycles - ProductionProgress
  hours = max(0, remainingFuelCycles) * CycleDurationInHours
  ```

  If `CyclesFuelLasts <= 0`, the estimate is `float.MaxValue`.

- `GoodConsumingBuilding` `FillInput` applies when the target inventory has a `GoodConsumingBuilding` that consumes the
  cargo good with `GoodPerHour > 0`. It combines unreserved inventory stock and the building's internal supply:

  ```text
  hours = (UnreservedAmountInStock(goodId) + suppliesLeft) / GoodPerHour
  ```

- Manufactory `EmptyOutput` works only when the source inventory belongs to a `Manufactory` with `HasCurrentRecipe` and
  the cargo good is one of the current recipe products. It counts full future product cycles from unreserved output
  capacity:

  ```text
  futureCycles = UnreservedCapacity(product.Id) / product.Amount
  ```

  If `futureCycles <= 0`, time is `0` because output is already blocked for that product. Otherwise the estimate counts
  the current cycle completion first, then each additional available product slot as another full cycle:

  ```text
  hours = RemainingCycleHours + (futureCycles - 1) * CycleDurationInHours
  ```

  If product amount is `<= 0`, the estimate is `float.MaxValue`.

These calculations use integer cycle counts for ingredients and products. Partial ingredients or partial output
capacity do not count as a full future cycle.

The current prototype classifies time estimates with fixed urgency windows, not delivery ETA. Delivery ETA is computed
for passive scoring, but readiness does not yet compare delivery ETA against time-to-critical. The intended model is
still time-to-critical versus delivery ETA: if a manufactory can keep working for about 2 days but delivery takes 1.5
days, the order may need to become dispatchable now even if the inventory is not past a fixed fill threshold. This is
SmartHaulers-owned design direction, not vanilla behavior.

The estimate does not fully model production efficiency, power availability, paused or blocked state, worker
availability, recipe switching beyond the current recipe, or other production limiters. If any readiness constants or
`CriticalTimeInHours` formulas are tuned, update this design note in the same change.

## FillInput Weight

SmartHaulers intentionally does not use vanilla overall input inventory fill as the final `FillInput` priority.

For each input good, the current prototype weight is:

```text
weight = 1 - AmountInStock(goodId) / LimitedAmount(goodId)
```

This makes a missing ingredient urgent even when another ingredient fills much of the same input inventory.

Example:

```text
Logs:  8 / 8  -> weight 0
Water: 0 / 3  -> weight 1
```

This is SmartHaulers-owned logic, not vanilla behavior.

For examples with `LimitedAmount(goodId) = 8`:

```text
0 / 8 -> weight 1
4 / 8 -> weight 0.5
7 / 8 -> weight 0.125
8 / 8 -> weight 0
```

The current take-away per-good expansion uses the inverse formula:

```text
weight = AmountInStock(goodId) / LimitedAmount(goodId)
```

The value is clamped to `[0, 1]`. If `LimitedAmount(goodId) <= 0` and stock exists, weight is `1`; this makes unwanted
or disallowed stock urgent instead of permanently deferred.

For examples with `LimitedAmount(goodId) = 20`:

```text
0 / 20  -> weight 0
10 / 20 -> weight 0.5
20 / 20 -> weight 1
```

Construction order weight currently uses building priority:

```text
weight = ((int)priority + 1) / 5
```

Examples:

```text
VeryLow  -> 0.2
Low      -> 0.4
Normal   -> 0.6
High     -> 0.8
VeryHigh -> 1.0
```

Current vanilla/fallback baseline weights:

- `RemoveUnwantedStock = 0.5`;
- `EmptyInventories = 0.51`;
- `BringNutrient`, `ObtainGood`, `SupplyGood`, and non-expanded vanilla `EmptyOutput` use the vanilla weighted behavior
  value as input or fallback, even when SmartHaulers uses its own candidate tier model for `ObtainGood` and
  `SupplyGood`.

## Passive Decisions

The current dispatcher computes passive decisions for `Dispatchable` orders only.

It picks a winner and runner-up without changing game state. The decision is diagnostic and explainable.

Candidate score currently uses:

```text
score = pickup ETA + delivery ETA + state penalty + capacity penalty
```

Where:

- pickup ETA is agent-to-source distance divided by agent speed;
- delivery ETA is source-to-target distance divided by agent speed;
- state penalty is small for idle-ish states;
- capacity penalty is a small penalty when the agent cannot carry the full requested cargo by weight.

Current pickup and delivery ETA values are computed as `distance / agent.Speed`. These values are real seconds of
movement, not in-game hours. They are usable for relative candidate ranking, but must be converted before comparing
against `CriticalTimeInHours` or presenting them as game-time ETA.

Capacity is weight-based. `GoodCarrier.LiftingCapacity` is kilograms, while cargo amount is item units. Passive scoring
must convert through the good's `GoodSpec.Weight` or the vanilla carry-amount semantics before deciding whether an agent
can carry the full request.

`Weight` is not currently added to the candidate score. The current score is an ETA and penalty comparison for already
`Dispatchable` candidates, not a weighted-priority sort across all planned orders.

Diagnostics should make this visible when useful, for example by showing carried amount and kilogram coverage such as:

```text
cap=4x 12/20kg (..., max=14kg)
```

This is deliberately simple. It is a comparison scaffold, not the final dispatch policy.

## Critical Inventory Needs

Critical self-supply needs are transport-like but separate from hauling orders. A worker or bot can travel to an
inventory to satisfy its own critical need, and that trip consumes time and path capacity, but it is not a haul-provider
request and should not be modeled as a normal delivery order.

The current SmartHaulers prototype handles only initial action selection for critical-state inventory needs. It patches
vanilla `DistrictNeedBehaviorService.PickBestAction` for `NeedFilter.OnlyCriticalStateNeeds`, considers
`InventoryNeedBehavior` targets, and currently applies to `Hunger`, `Thirst`, `Biofuel`, and `Power`. `Catalyst` is
excluded for now.

For those survival/refuel cases, the prototype ranks candidate inventory targets by nearest travel time and ignores
vanilla consumable quality/appraisal. Vanilla first chooses the highest-appraised consumable effect group, then chooses
the shortest action within that group; SmartHaulers deliberately prefers nearest survival/refuel access for critical
state.

This does not reroute an already running need trip. Vanilla `InventoryNeedBehavior` reserves exactly one good inside
the chosen inventory, and once a `WalkInsideExecutor` is running, current SmartHaulers does not cancel, retarget, or
reselect it. Mid-route retargeting requires a separate cancellation/reselection mechanism and remains unresolved.

## Distance Model

The current implementation uses public navigation and inventory APIs.

Known constraints:

- vanilla inventory picker returns one closest matching inventory, not an ordered list;
- repeated picker calls with exclusion filters can emulate "next inventory" but are not a scalable batch planner;
- multi-access buildings require care, because some vanilla helpers assume a single access point;
- cached road flow-field data may be unavailable immediately after save load, so diagnostics should prefer unknown
  endpoints or best-effort path estimates over crashes;
- inverted road-distance ranking may be useful later to align with flow-field cache origins.

## Diagnostics UI

The diagnostics UI is part of the design, not just a debug convenience.

It should support the investigation loop:

- see all district agents and orders;
- filter agents vs orders;
- inspect orders exposed by a selected building;
- inspect active order and state for a selected agent;
- click known objects and agents to select them in-game;
- show passive `best` and `next` candidates for dispatchable orders.

The UI is intentionally compact. Labels like `good=`, `beh=`, and `prog=` were removed when the values became
self-explanatory. `Queued` and `Covered` hide route text because it is misleading at those phases.

Diagnostics must keep game-assigned work and SmartHaulers planned candidates visually distinct. `GAME` rows are already
reserved or assigned by Timberborn. `IDEA` rows are SmartHaulers snapshot-bound planned candidates, and `IDEA/build`
rows are planned construction candidates. `IDEA` rows are not real assignments and should not be blamed on vanilla
workers until SmartHaulers actually intervenes.

Current main dispatch views are:

- `Agents`;
- `Orders`;
- `Perf`.

There is no `All` mode in the current UI. `Perf` shows a multi-line timing breakdown for the snapshot refresh loop,
including total time, pickup path, delivery path, agents, active orders, queued orders, construction, readiness,
decisions, sort, and other work.

## Current Limitations

The current design does not yet:

- reserve or assign orders;
- prevent vanilla from selecting a different worker;
- compare time-to-critical against delivery ETA for readiness;
- model compound plans across several source inventories;
- model compound plans across several target inventories;
- virtually subtract stock or capacity across planned segments;
- batch-plan several assignments from one snapshot;
- evaluate critical needs, hunger, thirst, fuel, or rest risk;
- cancel or drop active work;
- allow user-controlled builder help for hauling or transport;
- use parallel computation.

These are future layers.

## Before Real Intervention Checklist

This checklist records prototype gaps that must be revisited before SmartHaulers starts making real dispatch decisions.
It is a working backlog, not a final design spec and not a blocker for further diagnostics.

- SmartHaulers still observes, estimates, and scores. It does not perform real assignments, reservations,
  cancellations, target overrides, or source overrides.
- The resource-output gap is proven but not solved. Vanilla can send specialized workers from resource or workplace
  outputs across the map to an `Obtain` stockpile while a nearby valid storage exists. Future policy must decide how to
  prevent this without breaking simple nearest-storage unload behavior.
- `ObtainGood` and `SupplyGood` soft tier planning is implemented but unproven by controlled in-game dispatch tests.
  `StockpilePriorityTierDistancePenalty = 40` is still a tuning hypothesis.
- Current pickup and delivery ETA values from `distance / agent.Speed` are real seconds, not in-game hours. Convert
  them before comparing with `CriticalTimeInHours` or showing them as game-time ETA.
- Time-based readiness is partial. It covers selected production, fuel, consumer, and output cases, but not all
  stockpile flows, resource outputs, non-recipe consumers or producers, paused or blocked state, power, workers,
  efficiency, or recipe-switching effects.
- Compound and batch planning are not implemented. The safe strategy remains: refresh a snapshot, choose one candidate,
  perform a real reservation or assignment, then rebuild affected candidates. Virtual subtraction is future
  batch-planning work.
- Urgent orders still need a batching policy. Critical consumer need means supply should start, but it does not prove
  that every newly available unit at a remote source should get a separate agent. Future logic should compare consumer
  time-to-critical, delivery ETA, source production or arrival rate, agent capacity, and already assigned deliveries.
- Batching policy must distinguish "will block soon" from "already blocked". For already-blocked production, evaluate
  restart viability or minimum useful restart batch instead of treating every tiny input delivery as urgent.
- Active `GAME` orders are observed but not controlled. There is no cancel-before-pickup, no drop or redirect after
  pickup, no repeat-assignment guard, and no critical-need policy yet.
- Critical inventory needs are handled only at initial critical action selection. Mid-route cancellation, retargeting,
  and reselection for already-running need trips remain unresolved.
- Agent policy remains draft. Builders, production-workplace idle workers, community workers, ordinary idle workers,
  work hours, contamination, no-work state, sleep, and other needs need explicit policy before intervention.
- The UI is diagnostic, not final player-facing UI. It explains why; a final intervention UI should emphasize what
  SmartHaulers did.
- Performance is measured but not optimized. Path and ranking costs need batching, caching, cadence control, and
  parallel-friendly DTO work later, but optimization should not outrun correctness unless it is cheap and simple.

## Near-Term Direction

The next design layer should stay passive unless there is a clear reason to intervene.

Useful next steps:

- continue refining per-good planning and decide which remaining behaviors should expand per good;
- expand time-to-critical readiness coverage and compare it against delivery ETA;
- design a compound or multi-candidate order model for multi-source or multi-target coverage;
- keep planned candidates explicitly tied to the snapshot that produced them;
- improve passive scoring with clearer ETA and capacity semantics;
- compare vanilla active assignments with SmartHaulers passive decisions;
- identify the narrowest safe intervention point for assignment, if passive comparison proves useful.
