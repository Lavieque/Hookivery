# ADR-0005 — Event fan-out and destination subscription

## Context
An event arrives for one source and has to reach every destination that wants that kind of event.
TASK-017 needs to tell apart events whose deliveries have been created from those still waiting.
TASK-018 needs the set of destinations applicable to an event. Both run on the hottest path.

## Options considered — subscription model
### Option A — String collection on `Destination` (`text[]`)
### Option B — `DestinationSubscription` junction entity
### Option C — Pattern matching with wildcards (`order.*`)

## Options considered — event processing state
### Option D — `Event.Status` as fan-out state only
### Option E — `Event.Status` as aggregate delivery outcome
### Option F — No column; derive from the deliveries

## Options considered — fan-out concurrency
### M1 — The transaction lock is the claim
### M2 — Lease column (as chosen for deliveries in ADR-0004)

## Decision
Option A, Option D, M1.

1. Event types live in a `text[]` column on `destinations`. No junction table until a subscription
   needs state of its own; that is the trigger for revisiting.
2. Uniqueness of the types inside the array is a domain rule, not a database constraint.
3. An empty subscription list means *no* event types, never all of them. Misconfiguration must
   fail closed: a destination that subscribes to nothing receives nothing.
4. An inactive destination subscribes to nothing. `Destination.SubscribesTo` returns false when
   `IsActive` is false, so callers cannot forget the check.
5. Event types are normalized to lowercase on write, on both sides of the match. Without this,
   `Order.Created` and `order.created` coexist and subscriptions silently fail to match.
6. `Destination.SourceId` is required. Without it, two sources using the same event type name
   would cross-deliver payloads between unrelated producers.
7. `Event.Status` means fan-out only: `Pending`, `Dispatched`, `NoDestinations`. Delivery outcomes
   are never written to it; a multi-destination event has no single outcome.
8. `NoDestinations` is terminal. Without it an event with no subscribers is re-read by the worker
   on every cycle, forever, and nothing reports it.
9. Fan-out claims rows with `FOR UPDATE SKIP LOCKED` and no lease (M1). Unlike delivery sending,
   fan-out performs no external I/O, so the whole operation fits in one short transaction and the
   row lock is the claim. The lease of ADR-0004 exists only because an HTTP attempt takes seconds.
10. A unique constraint on `(event_id, destination_id)` is the final guard against duplicate
    deliveries, independent of claiming (TASK-018, criterion 3).

## Consequences
- The domain rule `SubscribesTo` and the SQL filter of TASK-018 express the same rule twice.
  They diverge silently if one changes alone; TASK-033 tests both against the same cases.
- Filtering events by "failed" is not possible through `Event.Status`; failures are queried on
  `deliveries` (TASK-014, TASK-023).
