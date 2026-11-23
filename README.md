# Hookivery

Webhook delivery platform built with C# / .NET 10.
It receives events from producer systems, persists them in PostgreSQL, processes them asynchronously and delivers
them to external destinations with retries, idempotency, a Dead Letter Queue, the Outbox Pattern, observability and
Docker deployment.

> **Status:** in development — solution foundation.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Docker, with Compose (PostgreSQL runs in a container)

## Getting started

```bash
git clone https://github.com/Lavieque/Hookivery.git
cd Hookivery

# Local configuration (.env, appsettings.json and appsettings.Development.json are not versioned)
cp .env.example .env   # then set POSTGRES_PASSWORD to a value of your own
cp src/Hookivery.Api/appsettings.example.json src/Hookivery.Api/appsettings.Development.json
cp src/Hookivery.Worker/appsettings.example.json src/Hookivery.Worker/appsettings.Development.json
# then make the password in ConnectionStrings:Hookivery match POSTGRES_PASSWORD

docker compose up -d

dotnet restore Hookivery.slnx
dotnet build Hookivery.slnx
```

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `ConnectionStrings:Hookivery` | `appsettings.Development.json`, or the `ConnectionStrings__Hookivery` environment variable | Required. The API fails at startup, not on the first request, when it is missing. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `.env` | Read by `docker-compose.yml`. The container refuses to start if one is missing. |

`appsettings.json` holds no connection string. It is the base file for every environment, so a value placed there
follows the application into production. Per-environment values belong in `appsettings.<Environment>.json` or in
the environment itself.

## Database

PostgreSQL runs as the `postgres` service in [`docker-compose.yml`](docker-compose.yml), published on
**`127.0.0.1:5437`** (loopback only) and mapped to port 5432 inside the container. Credentials come from `.env`;
the service refuses to start if a variable is missing.

```bash
docker compose ps          # the service must reach "healthy", not just "running"
docker compose logs postgres
docker compose down        # stops the container, keeps the data
```

Data lives in the named volume `hookivery-pgdata`. `docker compose down -v` also removes that volume and deletes
the database, with no confirmation and no way back.

## Migrations

The schema is owned by EF Core migrations, not by init scripts. `HookiveryDbContext` lives in
`Hookivery.Infrastructure`, and `Hookivery.Api` is the startup project that supplies its configuration:

```powershell
dotnet tool update --global dotnet-ef   # or install --global, the first time

$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet ef dbcontext info --project src/Hookivery.Infrastructure --startup-project src/Hookivery.Api
```

`dotnet ef` does not read `launchSettings.json`. Without `ASPNETCORE_ENVIRONMENT=Development` the host starts as
`Production`, never loads `appsettings.Development.json`, and the command fails with
`Connection string 'Hookivery' is not configured.`

No migration exists yet: the first one is created together with the initial entity mappings.

## Running locally

```bash
# API — http://localhost:5034 (http profile) or https://localhost:7065 (https profile)
dotnet run --project src/Hookivery.Api

# Worker
dotnet run --project src/Hookivery.Worker
```

In the `Development` environment, the API exposes the OpenAPI document at `/openapi/v1.json`.

## Running the tests

The integration tests reach the running container through `ConnectionStrings__Hookivery` — the same name the
application's configuration uses:

```powershell
$env:ConnectionStrings__Hookivery = "Host=localhost;Port=5437;Database=hookivery;Username=hookivery;Password=<yours>"

dotnet test Hookivery.slnx
```

Without that variable the integration tests fail with `Set ConnectionStrings__Hookivery before running this test.`

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
│   ├── architecture/                # Layer responsibilities and dependency rules
│   └── decisions/                   # Architecture Decision Records (ADRs)
├── .editorconfig                    # Code style and naming rules
├── .env.example                     # Template for the database credentials in .env
├── Directory.Build.props            # Shared MSBuild properties (nullable, analyzers)
├── Directory.Packages.props         # Centralized NuGet package versions
├── docker-compose.yml               # PostgreSQL service
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

- Branches: `main` (stable) and `feature/TASK-XXX-description`.
- Commits: [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.

## Architecture decisions

Relevant decisions are recorded as ADRs in [`docs/decisions/`](docs/decisions/).
