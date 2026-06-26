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

`WorkplaceIdle` is too broad for passive scoring by itself. The current SmartHaulers-owned scoring model classifies the
workplace role:

- transport workplace idle: district-center or hauling-center style workers; usable helper, small penalty;
- builder workplace idle: avoid unless useful or important; builders are idle now but expected to build;
- production workplace idle: manufactory or factory worker; avoid more strongly because production may resume as soon
  as inputs, power, or another blocker becomes available;
- unknown workplace idle: worst fallback because the interrupted work is unknown.

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
- `Covered`: the request appears covered by existing reservations or deliveries.
- `PickingUp`: an agent has an active transport order and is moving toward the source.
- `Delivering`: an agent has picked up cargo and is moving toward the target.

`Queued` and `Covered` are not route contracts. `Queued` has no useful route. `Covered` may be covered by multiple
agents, sources, or targets, so active agent rows are the reliable source for concrete source-to-target pairs.

## Possible Order Planning

The current possible-order planner expands vanilla haul-provider requests into SmartHaulers order snapshots.

For most supported behaviors, one vanilla request still produces at most one SmartHaulers order:

- `BringNutrient`;
- `EmptyInventories`;
- `EmptyOutput`;
- `ObtainGood`;
- `RemoveUnwantedStock`;
- `SupplyGood`.

`FillInput` is different. SmartHaulers now expands it per input good:

- one `FillInput` request may create multiple `Estimated` or `Covered` orders;
- each input good gets its own cargo and weight;
- goods are no longer prioritized by vanilla `GoodAmountComparer`;
- priority is represented by each order's weight.

The current planner still uses vanilla nearest inventory lookup. This means each planned good gets at most one nearest
source-to-target segment. It does not yet build compound coverage from multiple source inventories.

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

## Passive Decisions

The current dispatcher computes passive decisions for `Estimated` orders only.

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

Capacity is weight-based. `GoodCarrier.LiftingCapacity` is kilograms, while cargo amount is item units. Passive scoring
must convert through the good's `GoodSpec.Weight` or the vanilla carry-amount semantics before deciding whether an agent
can carry the full request.

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
- inverted road-distance ranking may be useful later to align with flow-field cache origins.

## Diagnostics UI

The diagnostics UI is part of the design, not just a debug convenience.

It should support the investigation loop:

- see all district agents and orders;
- filter agents vs orders;
- inspect orders exposed by a selected building;
- inspect active order and state for a selected agent;
- click known objects and agents to select them in-game;
- show passive `best` and `next` candidates for estimated orders.

The UI is intentionally compact. Labels like `good=`, `beh=`, and `prog=` were removed when the values became
self-explanatory. `Queued` and `Covered` hide route text because it is misleading at those phases.

## Current Limitations

The current design does not yet:

- reserve or assign orders;
- prevent vanilla from selecting a different worker;
- model compound plans across several source inventories;
- virtually subtract stock or capacity across planned segments;
- evaluate critical needs, hunger, thirst, fuel, or rest risk;
- cancel or drop active work;
- use parallel computation.

These are future layers.

## Near-Term Direction

The next design layer should stay passive unless there is a clear reason to intervene.

Useful next steps:

- expand per-good planning beyond `FillInput` where it makes sense;
- design a compound order model for multi-source or multi-target coverage;
- improve passive scoring with clearer ETA and capacity semantics;
- compare vanilla active assignments with SmartHaulers passive decisions;
- identify the narrowest safe intervention point for assignment, if passive comparison proves useful.
