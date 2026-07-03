# SmartHaulers Design Draft

This is an early design draft for the SmartHaulers prototype.

It is not a finished specification. It captures the SmartHaulers-owned model that is already partially implemented or
strong enough to guide the next implementation steps. Confirmed vanilla behavior and investigation details remain in
`developer-notes.md`.

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

Current phases:

- `Queued`: a vanilla request exists, but SmartHaulers has not found a concrete executable route and cargo.
- `Estimated`: SmartHaulers found a concrete source, target, and cargo that could be executed if started now.
- `Deferred`: SmartHaulers found a possible route and cargo, but the order is not urgent enough to dispatch yet.
- `Dispatchable`: SmartHaulers found a possible route and cargo that should be considered by passive decision scoring.
- `Covered`: the request appears covered by existing reservations or deliveries.
- `PickingUp`: an agent has an active transport order and is moving toward the source.
- `Delivering`: an agent has picked up cargo and is moving toward the target.

`Queued` and `Covered` are not route contracts. `Queued` has no useful route. `Covered` may be covered by multiple
agents, sources, or targets, so active agent rows are the reliable source for concrete source-to-target pairs.

Current phase and tuning cheat sheet:

- `Queued`: a vanilla request exists, but SmartHaulers did not find a concrete route or cargo. Weight may exist, but
  cargo can be `0x`; passive scoring does not evaluate it.
- `Estimated`: a concrete source, target, and cargo candidate exists, but readiness has not made it urgent enough for
  scoring. With a time estimate, `4h < t <= 12h` stays `Estimated`.
- `Deferred`: the candidate is possible but not urgent; passive scoring does not evaluate it. With a time estimate,
  `t > 12h` becomes `Deferred`.
- `Dispatchable`: the only planned-order phase considered by passive decision scoring. With a time estimate,
  `t <= 4h` becomes `Dispatchable`.
- `Covered`: existing reservations or deliveries appear to cover the request. It is not a single dispatcher route
  contract and passive scoring does not evaluate it.
- `PickingUp` and `Delivering`: active reservation or carry orders reconstructed from an agent. Weight is not used to
  select them.

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

The producer fallback for `ObtainGood` is deliberately narrow. Resource workplaces such as scavenger flags, gatherer
flags, lumberjack flags, farmhouses, or forester outputs should not become generic stockpile balancing sources again.

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
