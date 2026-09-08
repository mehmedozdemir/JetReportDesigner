# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/); versioning is [SemVer](https://semver.org/).

## [Unreleased]

### Added — Phase 3 (REST + SQL data sources)
- **REST connector** (`RestDataSourceReader`): GET, `{param:name}` in URL / query /
  headers, dotted `resultPath`, schema inference. **SSRF guard** (`SsrfGuard`):
  http/https only; a `SocketsHttpHandler` connect callback re-resolves DNS and
  rejects any loopback / private / CGNAT / link-local / ULA / multicast address
  (blocks `169.254.169.254` and RFC1918); redirects off; response + timeout caps;
  optional `DataSources:Rest:AllowedHosts`.
- **Registered connections**: `IConnectionRepository` over `StoredConnection`,
  connection strings encrypted with ASP.NET Data Protection
  (`IConnectionSecretProtector`); `ConnectionsController` CRUD that never returns
  the secret.
- **SQL connector** (`SqlDataSourceReader`): resolves the report's named connection
  ref, opens the matching ADO.NET provider (SQL Server / PostgreSQL / Oracle),
  runs a parameterised command (bound, never concatenated), maps rows and infers
  types, enforces a row cap + timeout, SELECT/WITH-only single statement
  (`SqlCommandGuard`).
- **Oracle** brought online: `Oracle.EntityFrameworkCore` 10.x,
  `Storage.Migrations.Oracle` + provider + migration (NCLOB for the JSON/secret
  columns), registered in the API; opt-in tests + `docker-compose --profile oracle`.
- **Designer**: data-source wizard (JSON / REST / SQL / None) with query/header
  editors, a connections manager, and a "Preview & load fields" action; parameters
  panel + a runtime parameter bar feeding preview and export.
- **Expression evaluator**: element values starting with `=` (arithmetic,
  comparison, `and`/`or`/`not`, `if`, `coalesce`, `format`, `upper`/`lower`/`len`,
  `pageNumber`/`totalPages`/`now`, field and `param.*` refs).
- **Fonts**: `SystemFontResolver` (OS fonts on Windows, Liberation/DejaVu on
  Linux); the container installs `fonts-liberation` + `libfontconfig1`.
- `IDataSourceReader` now takes a `DataSourceReadContext` (parameters + connection
  refs); data-source resolution and the render service are scoped.
- Verified end to end in the browser: a banded report bound to a live PostgreSQL
  query renders and re-runs when its runtime parameter changes; the stored
  connection string is ciphertext.

### Added — Phase 2 (banded reports)
- **Rendering**: `BandedLayoutBuilder` — single-level grouping (sorted), detail
  iteration, group/page/report aggregates (`AggregateComputer`), page
  header/footer, two-pass pagination with fixed-height bands, group-header repeat
  on page break, report footer. `ElementEmitter` (element→primitives) extracted
  and shared with the free-layout builder. `TableEmitter` — header + one row per
  data-source row with grid lines, usable in both layouts. `BindingResolver` gains
  `pageNumber()` / `totalPages()` / `now()` tokens and `ResolveGroupKey`.
  `ReportRenderService` renders banded reports (the `501` is gone).
- **API**: `/api/render` and `/api/reports/{id}/render` now serve banded reports.
- **Designer**: Free/Banded layout toggle (seeds default bands); band strip editor
  on the canvas (labelled tag, per-band height resize, drop targets); "+ band"
  menu; band inspector (height, detail data source, group expression + sort,
  repeat-on-every-page); element inspector gains aggregate function + scope inside
  footer bands. Table element: toolbox entry, canvas preview, columns editor (data
  source, header row, per-column header / binding / width / align / format).
  Generic element addressing across the body and all bands; undo/redo, keyboard
  and selection carried over.
- Verified end to end in the browser: a 45-row report grouped by customer
  paginates (Page 1/2, 2/2) with correct per-group subtotals and a report grand
  total; a free-layout table renders every data row with formatted values.

### Deferred within Phase 2
- Expression evaluator (`if`, arithmetic, string concat) — Phase 2 verification
  does not require it.
- Nested grouping, auto-height / can-grow bands, page-scoped aggregates inside a
  group footer, per-report culture.

### Added — Phase 1 (free-layout designer + static JSON + PDF)
- **Rendering pipeline**: `BindingResolver` (`{ds.field}` / `{param:name}` +
  .NET format strings), `ParameterValues`, `JsonRows` (dotted `resultPath`,
  scalar mapping, field-type inference), `ReportDataResolver`, `PageGeometry`,
  `EffectiveStyle`, `FreeLayoutBuilder` (report + data → `RenderDocument`),
  `HtmlReportRenderer`, and `ReportRenderService` (PDF + HTML).
- **API**: `POST /api/render`, `/api/reports/{id}/render`, `/api/reports/{id}/preview`,
  `/api/datasources/schema`, `/api/datasources/preview`; `NotSupportedException`
  → `501` for banded reports until Phase 2.
- **Designer** (`web/`): drag/click toolbox (label, field, rectangle, line, image,
  page-info); canvas with page geometry, zoom, grid, margin guides; pointer
  move/resize with grid snap; click + marquee selection; properties panel
  (bounds, text/binding, format, font, colour, background, border, alignment) and
  page setup; JSON data panel with schema load and drag-to-bind field chips;
  server-rendered HTML preview tab; Export PDF; undo/redo history, arrow-nudge,
  Delete, Ctrl+Z/Y, Ctrl+S.
- Render-model alignment enums renamed to `*Anchor` to avoid colliding with
  `Core.Model.TextAlign` / `VerticalAlign`.

### Deferred within Phase 1
- Embedding Liberation fonts + a cross-platform `IFontResolver` (tracked in
  `docs/04`); PDF rendering is currently Windows-only via `WindowsCoreFontResolver`.
- Copy/paste, alignment guides, in-canvas text editing, multi-page free flow,
  image rendering in PDF.

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
