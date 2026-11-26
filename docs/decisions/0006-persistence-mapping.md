# ADR-0006 — Persistence mapping of the domain model

## Context
TASK-007 maps the domain model to PostgreSQL. Several mapping choices change behavior, not just storage.

## Decision
1. `events.payload` is `text`, not `jsonb`. `jsonb` normalizes the document (key order, whitespace),
   so the stored bytes would differ from the signed bytes and HMAC verification (TASK-012) and replay
   (TASK-027) would break.
2. Enums are stored as strings. The claim SQL of ADR-0004 and its partial index filter on
   `status = 'Pending'`; integer values would also silently change meaning if an enum is reordered.
   A check constraint built from `Enum.GetNames` restricts each column to the known values.
3. `destinations.event_types` is a `text[]` column (ADR-0005, decision 1).
4. Foreign keys use `ON DELETE RESTRICT`, except `delivery_attempts -> deliveries` (`CASCADE`):
   attempts have no meaning without their delivery, events must not vanish with their source.
5. Domain limits are repeated as check constraints, generated from the domain constants, because
   raw SQL (fan-out, claiming) bypasses the domain.
6. Signing secrets are stored in plain text until TASK-035 decides on encryption at rest.

## Consequences
- Adding an enum member changes the model and requires a migration (the check constraint changes).
- Querying inside the payload needs a cast (`payload::jsonb`); no index on payload content for now.
- Database dumps contain usable secrets until TASK-035.
