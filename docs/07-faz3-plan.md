# Phase 3 — REST + SQL data sources

> Goal: bind reports to live data — an HTTP JSON endpoint or a parameterised SQL
> query against a registered database connection (SQL Server / PostgreSQL /
> Oracle) — with credentials stored encrypted and the obvious abuse vectors
> (SSRF, non-SELECT SQL, runaway result sets) closed off. Plus runtime parameters
> and a designer data-source wizard.

## Slices (each builds + a check, then a commit)

- **A — REST connector.** `RestDataSourceReader`: GET only, `{param:name}` in
  URL / query / headers, dotted `resultPath`, schema inference (reuses
  `JsonRows`). **SSRF guard**: http/https only; a `SocketsHttpHandler`
  `ConnectCallback` rejects connections whose resolved IP is loopback /
  private / link-local / ULA / multicast (blocks `169.254.169.254` and friends);
  redirects disabled; response size cap; per-request timeout. Optional
  `DataSources:Rest:AllowedHosts` allow-list. Check: reader parses a local test
  server's JSON; SSRF unit tests reject internal targets.
- **B — Registered connections.** `StoredConnection` CRUD (`IConnectionRepository`);
  connection strings encrypted at rest via an `IConnectionSecretProtector`
  (implemented in the API with ASP.NET Data Protection). `ConnectionsController`:
  list (id / name / provider only — never the string), create, update, delete,
  test (`SELECT 1`). Check: integration test — ciphertext ≠ plaintext, list omits
  the secret, test succeeds against a container DB.
- **C — SQL connector.** `SqlDataSourceReader`: opens an ADO.NET connection for the
  ref'd registered connection (`Microsoft.Data.SqlClient` / `Npgsql` /
  `Oracle.ManagedDataAccess.Core`), runs a **parameterised** command, maps rows,
  infers schema, enforces `maxRows` + `timeoutSeconds`, and a **SELECT-only**
  heuristic (`^\s*(SELECT|WITH)\b`, single statement). Report `connections[]` maps
  a name → connection id. Check: Testcontainers SQL Server + PostgreSQL — seed a
  table, read it through a SQL data source; a non-SELECT command is refused.
- **D — Oracle bring-up.** Verify `Oracle.EntityFrameworkCore` on net10; add
  `JetReportDesigner.Storage.Migrations.Oracle` + provider + `InitialCreate`;
  register in the API; add Oracle to `docker-compose` (optional service). Check:
  CI migrations job builds the Oracle assembly; storage round-trip test against an
  Oracle container (nightly / opt-in).
- **E — Designer: data-source wizard + parameters.** DataPanel becomes a
  kind-aware wizard (JSON / REST / SQL) with a live "Preview" (`/api/datasources/preview`);
  a small connections manager; a parameters editor and a "run parameters" prompt
  feeding `/api/render`. Check: build a REST-bound and a SQL-bound report in the
  browser and preview them with runtime parameters.
- **[done] F — Cleanup.**
  - Expression evaluator (`ExpressionEvaluator`): values prefixed with `=` are
    parsed and evaluated — numbers / strings / `true`/`false`/`null`, field and
    `param.*` references, `+ - * / %`, comparison, `and`/`or`/`not`, parentheses,
    and `if` / `coalesce` / `format` / `upper` / `lower` / `len` / `pageNumber` /
    `totalPages` / `now`. Wired into `BindingResolver.ResolveValue`. 16 tests.
  - `SystemFontResolver` replaces `WindowsCoreFontResolver` — OS fonts on Windows,
    Liberation/DejaVu on Linux; the container image installs `fonts-liberation` +
    `libfontconfig1`. Closes the `docs/04` follow-up.

## Security notes

- Connection strings: encrypted with ASP.NET Data Protection; never returned by
  the API; not written to logs.
- REST: SSRF guard as above; request headers/URLs with resolved parameter values
  are not logged verbatim.
- SQL: parameters always bound, never string-concatenated; SELECT-only; row cap
  and command timeout enforced server-side. A read-only DB account is the
  documented recommendation.

## Phase 3 verification

A report bound to a live REST endpoint and another bound to a SQL query both
render (PDF + HTML) with runtime parameters (e.g. a date range). The stored
connection string is ciphertext in the database. SSRF and non-SELECT attempts are
rejected.
