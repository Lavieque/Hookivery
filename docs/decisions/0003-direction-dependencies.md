# ADR-0003 — Dependency direction between layers

**Status:** Accepted
**Date:** 2026-11-19
**Related task:** TASK-003

## Context

The solution has five projects under `src/` (Api, Application, Domain, Infrastructure, Worker) and three test
projects. Who may reference whom has to be settled before any persistence code (TASK-005) or HTTP delivery code
(TASK-019) exists. Once the `DbContext` and the use cases are in place, reversing the direction of the references
stops being a change to `.csproj` files and becomes a rewrite.

Constraints:

- In Hookivery the structure has to make the rules visible, not merely work.
- The domain holds rules with value of their own, testable in isolation: `Delivery` state transitions, computing the
  next attempt, and the attempt limit before the Dead Letter Queue.
- PostgreSQL is both the source of truth and the work queue. The Outbox (TASK-026) and delivery claiming (TASK-024)
  require explicit transactional control and SQL with locking semantics, which makes it relevant where the knowledge
  about transactions lives.
- Two hosts consume the same logic: `Hookivery.Api` and `Hookivery.Worker`.

## Options considered

### Option A — Classic layering: Api → Application → Infrastructure → Domain

Application references Infrastructure and calls repositories and the `DbContext` directly.

- Pros:
  - Less indirection; the code reads top-down without having to look up implementations.
  - Requires no interface and no DI registration for each new dependency.
- Cons:
  - Application becomes coupled to the persistence technology; testing a use case then requires a database or a
    double of a concrete type.
  - Domain tends to acquire EF Core attributes and types out of convenience, losing its isolation.
  - Replacing or adding an implementation (for example a second HTTP sending mechanism) forces a change in the layer
    that uses it.

### Option B — Dependency inversion: Api/Worker → Application ← Infrastructure, Application → Domain

Application declares the abstractions (`IEventRepository`, `IWebhookSender`, …); Infrastructure implements them and
references Application. `Api` and `Worker` reference both, but use Infrastructure only to register DI at startup.

- Pros:
  - Use cases are tested with doubles, without a database or the network.
  - The business rules live in a project with no external dependencies, which makes the rule automatically
    verifiable.
  - Supports two hosts (Api and Worker) over the same logic without duplication.
- Cons:
  - More ceremony: every new dependency means an interface, an implementation and a DI registration.
  - The rule "the Api does not use Infrastructure directly" is not enforced by the compiler, because the project
    reference exists for DI. It requires discipline or an architecture test.
  - Abstracting persistence badly hides the transactional semantics, which are essential in this project.

### Option C — A single project, no layers

- Pros: maximum initial speed; no boundary to maintain.
- Cons: the structure no longer makes the architectural rules visible; the separation between synchronous ingestion
  and asynchronous processing loses its structural boundary; and the dependency rule does not exist, so it cannot be
  tested.

## Decision

**Option B** is adopted.

Reference graph, as implemented in the `.csproj` files (commit `38dffc2`):

- `Hookivery.Api` → `Hookivery.Application`, `Hookivery.Infrastructure`
- `Hookivery.Worker` → `Hookivery.Application`, `Hookivery.Infrastructure`
- `Hookivery.Infrastructure` → `Hookivery.Application`
- `Hookivery.Application` → `Hookivery.Domain`
- `Hookivery.Domain` → no project references

Associated decisions:

1. **`Hookivery.Domain` does not depend on EF Core**, nor on any infrastructure package. It admits only BCL types,
   including `TimeProvider`, which is to be used instead of a custom clock abstraction.
2. **The abstractions live in Application**, not in Domain. Domain does not need to know that persistence exists.
3. **`Api` and `Worker` reference Infrastructure exclusively for composition**, at startup. No controller,
   middleware or processor uses Infrastructure types.
4. **Abstractions are created when the task that requires them arrives**, not ahead of time. The list of foreseen
   abstractions belongs in `docs/architecture/README.md` as a map, not as a contract.

## Consequences

### What becomes easier

- Testing domain rules and use cases without PostgreSQL, Docker or the network — relevant because the retry and
  backoff tests (TASK-022) need controllable time, not real waiting.
- Reusing the same logic in both hosts.
- Verifying the main rule automatically: it is enough to inspect the references of the Domain assembly.

### What becomes harder, and the debt taken on

- **EF Core mapping without attributes in Domain.** All configuration moves to the Fluent API in
  `Hookivery.Infrastructure/Persistence` (TASK-007). Entities with private setters require backing-field
  configuration; value objects require `ComplexProperty` or `OwnsOne`. This is more verbose than annotating the
  entity, and it is the direct cost of this decision.
- **Transactional semantics cross a boundary.** The Outbox (TASK-026) requires the event and the outbox row to be
  written in the same transaction, and delivery claiming (TASK-024) requires `FOR UPDATE SKIP LOCKED`. Whichever
  abstraction is chosen for data access has to expose this explicitly; hiding transactions behind generic
  repositories would lead to punching through the abstraction itself. That decision is deferred to its own ADR,
  before TASK-005.
- **Rule 3 is not enforced by the compiler.** A `using Hookivery.Infrastructure` in a controller compiles without a
  warning. Debt taken on: there is still no automated test in `Hookivery.ArchitectureTests`, even though the project
  exists and references all five layers. Verification is currently done by reading the `.csproj` files.
- Every new dependency costs three steps (interface, implementation, DI registration).

### When to revisit

- If rule 3 is broken in code and only caught in review: implement the architecture tests in
  `Hookivery.ArchitectureTests` and, should the rules move to namespace level and grow in number, evaluate
  `NetArchTest.Rules` or `ArchUnitNET` as a justified dependency.
- If the Fluent API mapping of value objects becomes disproportionate to the value of the isolation, reconsider
  rule 1 — but not the rule that Application does not depend on Infrastructure.
- If Application accumulates interfaces with a single implementation and no use in tests, revisit rule 4: it may be
  producing indirection without benefit.
