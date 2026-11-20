# ADR-0004 — Transactional semantics and data access ports

**Status:** Accepted
**Date:** 2026-11-20
**Related task:** TASK-005 (decided beforehand, as required by ADR-0003)

## Context

[ADR-0003](0003-direction-dependencies.md) settled the direction of the dependencies: Application declares the
ports, Infrastructure implements them. It deliberately left open **who controls the transaction boundary**, and
deferred that to this ADR, to be written before TASK-005.

PostgreSQL is not only the source of truth here, it is also the work queue. Two P0 criteria depend on semantics
that a generic repository hides:

- TASK-026: "the event and the outbox record are persisted in the same transaction".
- TASK-024: "two workers cannot claim the same delivery simultaneously".

Two concrete failures follow from leaving this to a generic `AddAsync` that commits on its own:

1. The event is committed, the outbox row is not, because the process dies between the two calls. The event is
   never delivered, no error is raised, and the stored data looks sound.
2. `GetPendingAsync()` returns the same rows to two workers, and the delivery goes out twice.

The decision cannot wait: TASK-005 chooses the data access model and TASK-007 builds the Fluent API mapping and
the migrations on top of it. Once migrations exist, changing this is a rewrite, not an edit.

## Options considered — transaction boundary

### Option A — Explicit `IUnitOfWork` in Application

Application opens and commits transactions, composing several ports inside one.

- Pros:
  - A use case can compose any set of ports in a single commit.
  - One abstraction instead of one per atomic operation.
- Cons:
  - It is the `DbContext` under another name; the port exists to expose a transaction, not a capability.
  - Transaction lifetime management moves into Application, and with it the handling of transient database
    errors (serialization failures, deadlocks).
  - Nothing in a use case's signature states which writes are atomic.

### Option B — Coarse-grained ports, one per atomic operation

Each port method is itself the transaction. Application never sees transactions; atomicity is a guarantee of the
contract, stated in the method name and its documentation.

- Pros:
  - The guarantee is visible at the call site, not buried in an orchestration.
  - Infrastructure is free to use raw SQL and locking where the operation needs it.
  - Application code cannot leave a transaction half-committed, because it never opens one.
- Cons:
  - Each atomic write is an interface with one implementation and one caller. This is in tension with the rule
    recorded in `docs/architecture/README.md` ("an abstraction with a single implementation and no use in tests is
    indirection without benefit"). The value here is not substitutability: it is that the method name carries the
    atomicity guarantee.
  - The failure mode is combinatorial growth: a use case needing event, outbox and deliveries in one commit
    invites a `SaveWithOutboxAndDeliveriesAsync`. A third composed port means the option has stopped being a
    design.
  - The guarantee cannot be verified with test doubles. A fake that writes two objects to memory always passes.
    Proving atomicity requires real PostgreSQL plus failure injection (TASK-028, TASK-033).

### Option C — Expose the `DbContext` to Application

Rejected on arrival: it breaks rule 2 of ADR-0003. Recorded here because it solves every problem above and the
comparison has to be honest about that.

## Options considered — claiming mechanism

### M1 — The lock is the claim

Open a transaction, `SELECT ... FOR UPDATE SKIP LOCKED`, perform the HTTP POST, update the row, commit.

- Pros: simplest to reason about; no extra columns; a dead worker releases its rows immediately.
- Cons:
  - Each in-flight delivery holds a pooled connection for the duration of the HTTP timeout (30s in TASK-019),
    sitting `idle in transaction`.
  - Long transactions on the hottest table in the system prevent `VACUUM` from reclaiming dead tuples.
  - The validity of the claim becomes the transaction timeout rather than a value we choose, which does not meet
    "claims have a time validity" (TASK-024).

### M2 — Lease column, short transaction

Claiming and sending are separate operations. The claim transaction lasts milliseconds and commits before the
delivery is attempted; a `claimed_until` column keeps other workers away afterwards.

- Pros:
  - Connections are never held across network I/O.
  - The lease duration is an explicit, measurable value.
  - Recovery of abandoned claims needs no extra code: an expired lease makes the row eligible again, so the claim
    query itself is the recovery mechanism required by TASK-025.
- Cons:
  - Requires `claimed_by`, `claimed_until` and `claim_count` columns, and a partial index.
  - Two protection mechanisms with different lifetimes have to coexist, which is easy to "simplify" wrongly later.
  - A paused worker can outlive its lease, so result writes need fencing.

### M3 — Compare-and-swap per row

`UPDATE ... WHERE id = @id AND (claimed_until IS NULL OR claimed_until < now()) RETURNING *`; zero rows affected
means another worker won.

- Pros: no CTE, no `SKIP LOCKED`, easy to read.
- Cons: one round trip per delivery instead of one per batch; under contention workers waste attempts on the same
  row because nothing steers them to different ones. Inadequate for the multiple concurrent workers required by
  TASK-021.

Advisory locks (`pg_try_advisory_lock`) were considered and rejected: they are session-scoped, they are not
queryable state, they cannot express "valid until", and they vanish on a crash without leaving a trace.

## Decision

**Option B** for the transaction boundary, and **M2** for claiming.

1. **Coarse-grained ports are the rule, and only for writes that require atomicity.** Queries (TASK-014,
   TASK-023) stay ordinary read repositories. Without this limit the interface count grows for reads as well.
2. **`IUnitOfWork` is not introduced now.** Trigger for revisiting: the third composed port, or a use case that
   genuinely needs to compose two existing ports in one commit.
3. **Claiming uses a lease, committed before the HTTP attempt.** `SKIP LOCKED` and the lease are both required and
   are not redundant: `SKIP LOCKED` keeps two workers from picking the same row *during* the claim transaction
   (milliseconds) by skipping locked rows instead of blocking on them; the lease keeps other workers away for the
   minutes that follow, after the lock is gone. Removing either one reintroduces duplicate claiming.
4. **The database clock is the only source of time for expiry.** `now()` (transaction start timestamp, server
   clock) decides what is expired, never the worker's `DateTime.UtcNow`: workers with skewed clocks would disagree
   about which claims are abandoned and recover the same delivery. All instants are `timestamptz`.
5. **Result writes are fenced by the lease.** A worker that lost its claim must not overwrite the outcome produced
   by the worker that now holds it.
6. **Isolation stays at `Read Committed`** (the PostgreSQL default). The correctness of claiming comes from
   `SKIP LOCKED` plus the lease, not from a higher isolation level. No operation currently requires
   `Repeatable Read` or `Serializable`.
7. **Raw SQL lives in `Hookivery.Infrastructure/Persistence/`**, never in Application. Fluent API does not cover
   `SKIP LOCKED`, so these statements are written by hand.
8. **Transient-error retry belongs to Infrastructure, and `EnableRetryOnFailure` is not enabled for now.** With
   explicit transactions, Npgsql's retrying execution strategy throws "The configured execution strategy
   'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions". If retrying is wanted later,
   each operation must be wrapped in `CreateExecutionStrategy().ExecuteAsync(...)`.
9. **Uniqueness conflicts are translated inside Infrastructure.** The unique constraint on
   `(SourceId, ExternalId)` (TASK-013) surfaces as a result value, not as an exception: `PostgresException` and
   SQLSTATE `23505` never cross into Application.

### Claim statement

```sql
WITH claimable AS (
    SELECT id
    FROM deliveries
    WHERE status = 'Pending'
      AND next_attempt_at <= now()
      AND (claimed_until IS NULL OR claimed_until < now())
    ORDER BY next_attempt_at
    LIMIT @batchSize
    FOR UPDATE SKIP LOCKED
)
UPDATE deliveries d
SET claimed_by    = @workerId,
    claimed_until = now() + @lease,
    claim_count   = d.claim_count + 1
FROM claimable c
WHERE d.id = c.id
RETURNING d.*;
```

`FOR UPDATE SKIP LOCKED` has to sit in the CTE's `SELECT`; it does not exist on `UPDATE`. `LIMIT` has to be inside
the CTE, otherwise the whole queue is locked and claimed. Rows skipped by `SKIP LOCKED` do not count towards
`LIMIT`, so under concurrency a worker may receive fewer than `@batchSize` rows while work remains: a short batch
does not mean an empty queue, and the worker must not treat it as one.

### Fencing statement

```sql
UPDATE deliveries
SET status = @status, next_attempt_at = @next, claimed_by = NULL, claimed_until = NULL
WHERE id = @id
  AND claimed_by = @workerId
  AND claimed_until > now();
```

Zero rows affected means the lease was lost while the attempt was in flight. The result is discarded and recorded
as such; it is not retried against the row.

### Ports

Created when the task that needs them arrives (ADR-0003, rule 4), not now.

```csharp
// src/Hookivery.Application/Abstractions/IEventIngestionStore.cs
/// <summary>Persists the event and its outbox row in a single transaction.</summary>
/// <returns>The stored event, or the existing one when (SourceId, ExternalId) is already present.</returns>
Task<IngestionResult> SaveWithOutboxAsync(Event evt, OutboxMessage message, CancellationToken ct);

// src/Hookivery.Application/Abstractions/IDeliveryClaimStore.cs
/// <summary>Claims up to <paramref name="batchSize"/> due deliveries for <paramref name="workerId"/>,
/// granting a lease of <paramref name="lease"/>. No two workers receive the same delivery.
/// Commits before returning: the lease, not a database lock, guarantees exclusivity.
/// A shorter result than <paramref name="batchSize"/> does not imply an empty queue.</summary>
Task<IReadOnlyList<Delivery>> ClaimBatchAsync(string workerId, TimeSpan lease, int batchSize, CancellationToken ct);

// src/Hookivery.Application/Abstractions/IDeliveryResultStore.cs
/// <summary>Records the attempt and the resulting delivery state in a single transaction.
/// The write is rejected when the worker no longer holds a valid lease on the delivery.</summary>
Task<bool> RecordAttemptAsync(string workerId, Delivery delivery, DeliveryAttempt attempt, CancellationToken ct);
```

### Schema this imposes

Decided here because it constrains TASK-006 and TASK-007.

| Column on `deliveries` | Type | Purpose |
|---|---|---|
| `status` | enum / text, not null | claim predicate and terminal states |
| `next_attempt_at` | `timestamptz not null` | backoff scheduling (TASK-022) |
| `claimed_by` | `text null` | worker identity, used for fencing |
| `claimed_until` | `timestamptz null` | claim validity |
| `claim_count` | `int not null default 0` | distinguishes retries from claim churn |

```sql
CREATE INDEX ix_deliveries_claimable
    ON deliveries (next_attempt_at)
    WHERE status = 'Pending';
```

Without the partial index the claim query sequentially scans the hottest table in the system.

### Lease duration

The lease must exceed the worst case of a single attempt: HTTP timeout plus DNS resolution plus margin. With the
30s timeout of TASK-019, two minutes is defensible; 30s guarantees duplicate delivery on every slow destination.
Too short duplicates deliveries, too long leaves work idle after a crash until it expires. There is no correct
value, only a configurable one that is measured (TASK-039: attempt duration distribution, expired-lease count).
Renewal of an in-flight lease is implemented only if measurement shows attempts exceeding the lease.

### What this does not solve

A worker can claim, POST, have the destination accept with 200, and die before recording the result. The lease
expires, another worker claims and delivers again. The destination sees the event twice.

This is not a defect of the mechanism: the project's rule is *at-least-once* plus idempotency, never exactly-once
over HTTP. The mitigation belongs to TASK-019 ("add the event identifier to the headers") so the destination can
deduplicate. No lease length removes this window.

## Consequences

### What becomes easier

- Atomicity is readable at the call site: `SaveWithOutboxAsync` says what it guarantees.
- Locking SQL stays inside Infrastructure; Application has no notion of `SKIP LOCKED`.
- Recovery after restart (TASK-025) is the claim query itself, with no reaper process to write or supervise.
- Connections are never held across network I/O, so the pool size follows concurrency, not destination latency.

### What becomes harder, and the debt taken on

- Atomicity can only be proven with real PostgreSQL and injected failures, not with doubles. TASK-028 and
  TASK-033 carry that weight.
- Several ports will have one implementation and one caller, against the general rule in
  `docs/architecture/README.md`. Accepted here, for the reason stated in decision 1.
- Handwritten SQL is outside the reach of the Fluent API mapping, so a renamed column breaks it at runtime rather
  than at compile time. Integration tests are the only guard.
- `EnableRetryOnFailure` stays off, so transient database errors surface to the worker's own retry handling.
- Fencing has to be applied on every write that follows a claim. One forgotten `claimed_by = @workerId` predicate
  reintroduces the lost-update window silently.

### When to revisit

- At the third composed port, or at the first use case that must compose two existing ports in one commit:
  evaluate Option A.
- If measurement shows attempts regularly exceeding the lease: implement lease renewal before enlarging the lease.
- If claim contention or queue latency becomes the bottleneck under load (TASK-044): reconsider batch size,
  index shape, and only then whether a broker-based queue (EPIC 08) is justified.
- If `Read Committed` proves insufficient for an operation added later: that operation gets its own ADR, rather
  than raising the isolation level globally.
