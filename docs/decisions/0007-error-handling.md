# ADR-0007 — Global error handling with Problem Details

## Status
Accepted

## Context
Unhandled exceptions must not leak SQL, hosts or secrets, and every error response must share one shape.

## Options
1. `IExceptionHandler` + `AddProblemDetails` + `UseExceptionHandler` (built into ASP.NET Core).
2. Custom try/catch middleware.
3. `UseExceptionHandler` with an inline lambda.

## Decision
Option 1. `GlobalExceptionHandler` is the last resort:
- Unknown exceptions → 500, generic title, no `detail`, logged as Error.
- `BadHttpRequestException` → its status code, logged as Warning.
- Request aborted by the client → 499, no body, logged as Information.
- `UseStatusCodePages` turns empty 4xx responses into Problem Details.
- Every response carries `traceId` to correlate with logs.

`ArgumentException` and `InvalidOperationException` are **not** mapped globally: the BCL and EF Core throw them
for bugs too, and mapping them to 400 would hide defects as client errors. Expected failures (validation, conflicts)
are mapped explicitly by each use case from TASK-009 on.

## Consequences
- No new runtime dependency.
- Expected errors need an explicit mapping per use case; the global handler never guesses.
