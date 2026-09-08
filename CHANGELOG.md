# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/); versioning is [SemVer](https://semver.org/).

## [Unreleased]

### Added — Phase 0 (skeleton, contracts, PDF spike)
- Solution scaffold: `Core`, `DataSources`, `Rendering`, `Storage`, two provider
  migration assemblies, `Api`, and Core/Rendering/Api test projects; central
  package management; shared build props; `.editorconfig`.
- `ReportDefinition` model (`Core`), embedded JSON Schema (draft 2020-12) served at
  `GET /api/meta/schema`, and a FluentValidation validator (layout-mode
  consistency, unique names, band ↔ data-source integrity).
- Canonical `ReportJson` options, shared by storage and the API so a report
  round-trips byte-for-byte (camelCase, string enums, nulls omitted).
- `Storage`: EF Core 10 `JetReportDbContext`, `Reports` / `Connections` entities,
  provider-agnostic optimistic concurrency token, `IReportRepository`, and a
  provider-selection seam (`IStorageProvider`) with SQL Server and PostgreSQL
  implementations + `InitialCreate` migrations.
- `Api`: reports CRUD (`GET`/`POST`/`PUT`/`DELETE`) with `ETag`/`If-Match`
  concurrency, RFC 7807 errors (`422` validation, `409` conflict), Serilog, CORS,
  health checks (`/health/live`, `/health/ready`), OpenAPI, and SPA static hosting
  with fallback.
- `Rendering`: engine-independent render model and `IPdfRenderer`, with QuestPDF
  and PdfSharp/MigraDoc implementations for the Phase 0 spike.
- `web/`: React + TypeScript + Vite shell — lists reports, creates a report,
  save round-trip, placeholder canvas.
- Tooling: `docker-compose.yml` (SQL Server + PostgreSQL), multi-stage
  `Dockerfile` (single container), GitHub Actions CI (build/test, migrations
  wiring check, frontend build).
- Docs: analysis & phased plan, schema reference, API contract, PDF-engine
  decision (**primary: PdfSharp/MigraDoc**; QuestPDF kept as a fallback behind
  `IPdfRenderer` through Phase 2).
