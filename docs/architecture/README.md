# Architecture

Hookivery is a modular monolith: one solution, five projects under `src/`, with the boundaries enforced by project
references rather than by convention alone.

This document is a **map**, not a contract. It records the responsibilities of each layer and the abstractions
foreseen for the tasks still to come, so the shape of the system is visible before the code exists. An abstraction
listed here is created only when the task that requires it arrives (ADR-0003, rule 4); until then it is a plan, and
changing or dropping it costs nothing.

The direction of the dependencies and the reasoning behind it are in
[ADR-0003](../decisions/0003-direction-dependencies.md).

## Layers

| Project | Responsibility | Must not contain |
|---|---|---|
| `Hookivery.Domain` | Entities (`Event`, `Source`, `Destination`, `Delivery`, `DeliveryAttempt`), their states and the rules with value of their own: state transitions, next-attempt computation, attempt limit before the Dead Letter Queue | EF Core, HTTP, any infrastructure package. BCL types only, `TimeProvider` included |
| `Hookivery.Application` | Use cases, orchestration across entities, and the abstractions (ports) that infrastructure implements | SQL, `DbContext`, `HttpClient`, Npgsql types |
| `Hookivery.Infrastructure` | Implementations of those ports: persistence (EF Core / PostgreSQL), HTTP delivery client, outbound integrations | Business rules; it adapts, it does not decide |
| `Hookivery.Api` | HTTP surface: event ingestion, management of sources and destinations, queries. Composition root | Business rules; direct use of Infrastructure types outside startup |
| `Hookivery.Worker` | Asynchronous processing: picking up pending work, creating deliveries, sending, retries. Composition root | Business rules; direct use of Infrastructure types outside startup |

Both hosts run the same Application logic. Nothing is duplicated between them.

## Dependency direction

```text
Api ─────┐
         ├──► Application ──► Domain
Worker ──┤
         └──► Infrastructure ──► Application
```

- `Domain` has no project references.
- `Application` references `Domain` only.
- `Infrastructure` references `Application` and implements its abstractions.
- `Api` and `Worker` reference both, but use `Infrastructure` **exclusively to register DI at startup**.

The last rule is not enforced by the compiler: the project reference exists for composition, so a `using
Hookivery.Infrastructure` in a controller compiles without a warning. It is verified by reading the `.csproj` files
today, and is meant to become a test in `Hookivery.ArchitectureTests`.

## Foreseen abstractions

Declared in `Hookivery.Application`, implemented in `Hookivery.Infrastructure`. Names are provisional; the task
column is the point at which each one is expected to be needed.

| Abstraction | Purpose | Introduced by |
|---|---|---|
| `ISourceRepository` | Reading and writing producer systems | TASK-009 |
| `IDestinationRepository` | Reading and writing destinations; selecting the active ones for an event | TASK-010, TASK-018 |
| `IEventRepository` | Persisting received events, deduplicating by source plus external id, querying | TASK-011, TASK-013, TASK-014, TASK-017 |
| `IDeliveryRepository` | Persisting deliveries and attempts, updating states, querying failures | TASK-018, TASK-020, TASK-023 |
| `IDeliveryClaimStore` | Claiming pending deliveries with a lease, releasing abandoned ones | TASK-024, TASK-025 |
| `IWebhookSender` | Sending the payload to a destination over HTTP and reporting status code, duration and failure kind | TASK-019 |
| `ISignatureVerifier` | HMAC verification of inbound requests | TASK-012 |
| `IOutboxStore` | Writing the outbox row in the same transaction as the event; marking rows as published | TASK-026 |
| Transaction boundary | Grouping writes that have to commit together | deferred, see below |
| `IEventPublisher` | Publishing to a broker | TASK-030, only if RabbitMQ is justified by then |

Not abstractions, deliberately:

- **Time.** `TimeProvider` from the BCL is used directly. No custom clock interface.
- **HTTP plumbing.** `IHttpClientFactory` is configured in Infrastructure (TASK-019); Application sees
  `IWebhookSender`, not the factory.
- **Retry and backoff computation.** This is a domain rule (next attempt, jitter, attempt limit), not a port.
  The policy's configuration values come from configuration; the computation stays in `Hookivery.Domain`
  (TASK-022, TASK-023).

## Open decision: transactional semantics

The Outbox (TASK-026) requires the event and its outbox row to be written in one transaction, and delivery claiming
(TASK-024) requires locking semantics such as `FOR UPDATE SKIP LOCKED`. PostgreSQL is both the source of truth and
the work queue, so these are not incidental details: whichever abstraction is chosen for data access has to expose
them explicitly. Hiding transactions behind generic repositories would force punching through the abstraction
later.

This is deferred to its own ADR, to be written before TASK-005. Until then, no repository abstraction above is
considered settled.

## Rules this structure exists to keep

- `Hookivery.Domain` stays free of external dependencies, so its rules are testable without PostgreSQL, Docker or
  the network.
- Use cases are testable with doubles.
- An abstraction with a single implementation and no use in tests is indirection without benefit; it gets
  revisited, not kept out of politeness.
- No new technology before a concrete technical need: RabbitMQ (EPIC 08) and Redis stay out until a task justifies
  them.
