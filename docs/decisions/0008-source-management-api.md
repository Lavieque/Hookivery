# ADR-0008 — Source management API

**Status:** Accepted
**Date:** 2026-11-27
**Related task:** TASK-009

## Context
Producers must be registered, inspected, renamed, deactivated and reactivated. Events reference sources with
`ON DELETE RESTRICT`, and a source's signing secret will verify inbound requests (TASK-012).

## Decisions
1. **`DELETE` deactivates, it does not delete.** Events keep their source, history stays queryable, and repeating
   the call returns 204 again. `PUT` with `isActive: true` reactivates. Hard delete was rejected: it either fails
   on the foreign key or erases history.
2. **The server generates the secret** (32 random bytes, base64url) and returns it only in the `POST` response.
   No read endpoint exposes it. Rotation is out of scope until a task needs it.
3. **Validation at the API boundary** with the built-in minimal API validation (`AddValidation`, DataAnnotations),
   answering `400` Problem Details. Domain checks stay as guards: if one fires, it is a bug and surfaces as `500`
   (ADR-0007).
4. **Pagination by `offset`/`limit`**, `limit` 1–100 (default 50), ordered by id (UUIDv7, creation order).
5. **One repository, each write commits on its own** (ADR-0004, option B): a source is a single aggregate.
6. Names are not unique: the id is the identity.

## Consequences
- The secret is stored as is, because HMAC verification needs the raw value; encryption at rest belongs to
  TASK-035.
- The endpoints are unauthenticated until TASK-036; run the API on localhost only.
- Concurrent `PUT`s are last-write-wins. Add an optimistic concurrency token (`xmin`) if this becomes a problem.
- Inactive sources are refused by `Event.Receive`; the HTTP response for that case is defined in TASK-011.
