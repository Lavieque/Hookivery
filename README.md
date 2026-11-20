# Hookivery

Webhook delivery platform built with C# / .NET 10.
It receives events from producer systems, persists them in PostgreSQL, processes them asynchronously and delivers
them to external destinations with retries, idempotency, a Dead Letter Queue, the Outbox Pattern, observability and
Docker deployment.

> **Status:** in development — solution foundation.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Docker (required from the PostgreSQL setup onwards)

## Getting started

```bash
git clone https://github.com/Lavieque/Hookivery.git
cd Hookivery

# Local configuration (appsettings.json files are not versioned)
cp src/Hookivery.Api/appsettings.example.json src/Hookivery.Api/appsettings.json
cp src/Hookivery.Worker/appsettings.example.json src/Hookivery.Worker/appsettings.json

dotnet restore Hookivery.slnx
dotnet build Hookivery.slnx
dotnet test Hookivery.slnx
```

Run locally:

```bash
# API — http://localhost:5034 (http profile) or https://localhost:7065 (https profile)
dotnet run --project src/Hookivery.Api

# Worker
dotnet run --project src/Hookivery.Worker
```

In the `Development` environment, the API exposes the OpenAPI document at `/openapi/v1.json`.

## Structure

```text
Hookivery/
├── src/
│   ├── Hookivery.Api/               # ASP.NET Core Web API: event ingestion and management
│   ├── Hookivery.Application/       # Use cases, orchestration and abstractions (ports)
│   ├── Hookivery.Domain/            # Entities and business rules, no external dependencies
│   ├── Hookivery.Infrastructure/    # Persistence (EF Core / PostgreSQL), HTTP client, integrations
│   └── Hookivery.Worker/            # Asynchronous processing and webhook delivery
├── tests/
│   ├── Hookivery.UnitTests/         # Unit tests (Domain, Application)
│   ├── Hookivery.IntegrationTests/  # Tests against real dependencies (e.g. PostgreSQL)
│   └── Hookivery.ArchitectureTests/ # Enforcement of the dependency rules between layers
├── docs/
│   └── decisions/                   # Architecture Decision Records (ADRs)
├── .editorconfig                    # Code style and naming rules
├── Directory.Build.props            # Shared MSBuild properties (nullable, analyzers)
├── Directory.Packages.props         # Centralized NuGet package versions
└── Hookivery.slnx                   # Solution
```

### Dependency direction (intended)

```text
Api ─────┐
         ├──► Application ──► Domain
Worker ──┤
         └──► Infrastructure ──► Application
```

- `Domain` depends on no other project and not on EF Core.
- `Application` depends only on `Domain`.
- `Infrastructure` implements the abstractions defined in `Application`.
- `Api` and `Worker` are *composition roots*: they wire the implementations through dependency injection.

## Conventions

### Language

All versioned content is written in English: code, comments, identifiers, documentation, ADRs, commit messages,
branch names, issues and pull requests.

### Naming

Defined in `.editorconfig`:

| Element | Convention | Example |
|---|---|---|
| Projects | `Hookivery.<Layer>` | `Hookivery.Domain` |
| Namespaces | Match the folder, *file-scoped* declaration | `namespace Hookivery.Worker;` |
| Types, methods, properties, events, enums | `PascalCase` | `DeliveryAttempt` |
| Interfaces | `I` prefix + `PascalCase` | `IDeliveryRepository` |
| Generic parameters | `T` prefix + `PascalCase` | `TEntity` |
| Private/protected fields | `_camelCase` | `_logger` |
| Private static fields | `s_camelCase` | `s_cache` |
| Constants and `static readonly` fields | `PascalCase` | `MaxAttempts` |
| Local variables and parameters | `camelCase` | `eventId` |
| Asynchronous methods | `Async` suffix and `CancellationToken` | `DeliverAsync(..., ct)` |

### Code

- `Nullable` and `ImplicitUsings` enabled in every project (`Directory.Build.props`).
- Analyzers: Meziantou.Analyzer, SonarAnalyzer.CSharp and Roslynator; style validated at build time (`EnforceCodeStyleInBuild`).
- In CI (`ContinuousIntegrationBuild=true`), warnings are treated as errors.
- Package versions managed only in `Directory.Packages.props` (Central Package Management);
  the `.csproj` files declare no versions.

### Git

- Branches: `main` (stable), `develop` (integration), `feature/TASK-XXX-description`.
- Commits: [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.

## Architecture decisions

Relevant decisions are recorded as ADRs in [`docs/decisions/`](docs/decisions/).
