# Hookivery

Plataforma de entrega de webhooks (Webhook Delivery Platform) em C# / .NET 10.
Recebe eventos de sistemas emissores, persiste-os em PostgreSQL, processa-os de forma assíncrona e entrega-os
a destinos externos com retries, idempotência, Dead Letter Queue, Outbox Pattern, observabilidade e deployment em Docker.

> **Estado:** em desenvolvimento — fundação da solução.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Docker (necessário a partir da configuração do PostgreSQL)

## Começar

```bash
git clone https://github.com/Lavieque/Hookivery.git
cd Hookivery

# Configuração local (os appsettings.json não são versionados)
cp src/Hookivery.Api/appsettings.example.json src/Hookivery.Api/appsettings.json
cp src/Hookivery.Worker/appsettings.example.json src/Hookivery.Worker/appsettings.json

dotnet restore Hookivery.slnx
dotnet build Hookivery.slnx
dotnet test Hookivery.slnx
```

Executar localmente:

```bash
# API — http://localhost:5034 (perfil http) ou https://localhost:7065 (perfil https)
dotnet run --project src/Hookivery.Api

# Worker
dotnet run --project src/Hookivery.Worker
```

Em ambiente `Development`, a API expõe o documento OpenAPI em `/openapi/v1.json`.

## Estrutura

```text
Hookivery/
├── src/
│   ├── Hookivery.Api/               # ASP.NET Core Web API: receção de eventos e gestão
│   ├── Hookivery.Application/       # Casos de uso, orquestração e abstrações (ports)
│   ├── Hookivery.Domain/            # Entidades e regras de negócio, sem dependências externas
│   ├── Hookivery.Infrastructure/    # Persistência (EF Core / PostgreSQL), cliente HTTP, integrações
│   └── Hookivery.Worker/            # Processamento assíncrono e entrega dos webhooks
├── tests/
│   ├── Hookivery.UnitTests/         # Testes unitários (Domain, Application)
│   ├── Hookivery.IntegrationTests/  # Testes com dependências reais (ex.: PostgreSQL)
│   └── Hookivery.ArchitectureTests/ # Verificação das regras de dependência entre camadas
├── docs/
│   └── decisions/                   # Architecture Decision Records (ADRs)
├── .editorconfig                    # Estilo de código e regras de nomenclatura
├── Directory.Build.props            # Propriedades MSBuild comuns (nullable, analyzers)
├── Directory.Packages.props         # Versões centralizadas dos pacotes NuGet
└── Hookivery.slnx                   # Solução
```

### Direção das dependências (pretendida)

```text
Api ─────┐
         ├──► Application ──► Domain
Worker ──┤
         └──► Infrastructure ──► Application
```

- `Domain` não depende de nenhum outro projeto nem de EF Core.
- `Application` depende apenas de `Domain`.
- `Infrastructure` implementa as abstrações definidas em `Application`.
- `Api` e `Worker` são *composition roots*: ligam as implementações através de injeção de dependências.

## Convenções

### Nomenclatura

Definida no `.editorconfig`:

| Elemento | Convenção | Exemplo |
|---|---|---|
| Projetos | `Hookivery.<Camada>` | `Hookivery.Domain` |
| Namespaces | Seguem a pasta, declaração *file-scoped* | `namespace Hookivery.Worker;` |
| Tipos, métodos, propriedades, eventos, enums | `PascalCase` | `DeliveryAttempt` |
| Interfaces | Prefixo `I` + `PascalCase` | `IDeliveryRepository` |
| Parâmetros genéricos | Prefixo `T` + `PascalCase` | `TEntity` |
| Campos privados/protegidos | `_camelCase` | `_logger` |
| Campos estáticos privados | `s_camelCase` | `s_cache` |
| Constantes e campos `static readonly` | `PascalCase` | `MaxAttempts` |
| Variáveis locais e parâmetros | `camelCase` | `eventId` |
| Métodos assíncronos | Sufixo `Async` e `CancellationToken` | `DeliverAsync(..., ct)` |

### Código

- `Nullable` e `ImplicitUsings` ativos em todos os projetos (`Directory.Build.props`).
- Analyzers: Meziantou.Analyzer, SonarAnalyzer.CSharp e Roslynator; estilo validado no build (`EnforceCodeStyleInBuild`).
- Em CI (`ContinuousIntegrationBuild=true`), os warnings são tratados como erros.
- Versões de pacotes geridas apenas em `Directory.Packages.props` (Central Package Management);
  os `.csproj` não indicam versões.

### Git

- Branches: `main` (estável), `develop` (integração), `feature/TASK-XXX-descricao`.
- Commits: [Conventional Commits](https://www.conventionalcommits.org/) — `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.

## Decisões de arquitetura

As decisões relevantes são registadas como ADR em [`docs/decisions/`](docs/decisions/).
