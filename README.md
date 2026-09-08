# JetReportDesigner

A web-based report designer and rendering engine — design reports in the browser
(drag-and-drop), bind them to REST / JSON / SQL data, and render to PDF. Inspired by
tools like the DevExpress Report Designer; built to grow from a focused V1.

> **Status: Phase 0 (skeleton).** The solution builds, the API persists report
> definitions to SQL Server / PostgreSQL via EF Core, the React shell lists and
> creates reports, and the PDF-engine spike is decided. The visual designer and
> rendering pipeline arrive in Phase 1+. See [docs/01-analiz-ve-plan.md](docs/01-analiz-ve-plan.md).

## Layout

```
src/
  JetReportDesigner.Core          Report model, JSON schema, validation
  JetReportDesigner.DataSources   REST / JSON / SQL connectors            (Phase 3)
  JetReportDesigner.Rendering     Render model + IPdfRenderer (+ spike engines)
  JetReportDesigner.Storage       EF Core DbContext, entities, repositories
  JetReportDesigner.Storage.Migrations.SqlServer
  JetReportDesigner.Storage.Migrations.PostgreSql
  JetReportDesigner.Api           ASP.NET Core 10 host
web/                              React + TypeScript + Vite designer
tests/                            Core / Rendering / Api test projects
docs/                             Analysis, schema, API contract, decisions
```

## Prerequisites

- .NET SDK 10 (`global.json` pins `10.0.400`)
- Node.js 20+
- Docker (for local databases and the integration tests)

## Run it locally

```bash
# 1. databases
docker compose up -d

# 2. API  (http://localhost:5067) — applies migrations on startup in Development
dotnet run --project src/JetReportDesigner.Api

# 3. designer  (http://localhost:5173, proxies /api to the API)
cd web && npm install && npm run dev
```

Development settings point at the `docker compose` SQL Server. To use PostgreSQL
instead, set `Storage:Provider=PostgreSql` and `Storage:ConnectionString`
(`Host=localhost;Port=5433;Database=jetreportdesigner;Username=jet;Password=jet`).
The compose file maps PostgreSQL to host port **5433** to avoid clashing with a
local 5432. Oracle is opt-in: `docker compose --profile oracle up -d`.

### Selected configuration keys

| Key | Meaning |
|-----|---------|
| `Storage:Provider` | `SqlServer` \| `PostgreSql` \| `Oracle` |
| `Storage:ReportStore` | `database` (default) \| `filesystem` (needs `Storage:FileSystemPath`) |
| `Storage:MigrateOnStartup` | apply pending EF migrations at boot |
| `DataProtection:KeyPath` | directory for the encryption key ring — set to a mounted volume in containers so registered connection secrets survive restarts |
| `DataSources:Rest:AllowedHosts` | optional allow-list for REST data source hosts |

## Test

```bash
dotnet test                     # Core + Rendering always; Api integration needs Docker
cd web && npm run build         # type-checks and builds the designer
```

## Build the container (API serving the built SPA)

```bash
docker build -t jetreportdesigner .
docker run -p 8080:8080 -e Storage__Provider=PostgreSql \
  -e Storage__ConnectionString="Host=...;Database=...;Username=...;Password=..." \
  -e Storage__MigrateOnStartup=true jetreportdesigner
```

## Docs

| Doc | |
|-----|-|
| [01-analiz-ve-plan.md](docs/01-analiz-ve-plan.md) | Analysis, architecture, phased plan, multi-agent split |
| [02-report-definition-schema.md](docs/02-report-definition-schema.md) | The `ReportDefinition` document |
| [03-api-contract.md](docs/03-api-contract.md) | HTTP endpoints |
| [04-pdf-motoru-karari.md](docs/04-pdf-motoru-karari.md) | PDF engine spike + decision |
