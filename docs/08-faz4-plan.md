# Phase 4 — Hardening & polish

> Goal: make V1 shippable. Versioned reports, an optional filesystem store, a
> designer that is comfortable to use, clear validation feedback, sample content,
> and the operational loose ends closed (Data Protection keys, load behaviour,
> the QuestPDF spike code removed).

## Slices — all complete

- **[done] A — Report versioning.** A `ReportVersions` table (per-provider migration): a
  snapshot (`version`, `definitionJson`, `savedAtUtc`, `name`) is written on every
  update. `IReportRepository` gains `ListVersionsAsync` / `GetVersionAsync` /
  `RestoreVersionAsync`. API: `GET /api/reports/{id}/versions`,
  `GET .../versions/{v}`, `POST .../versions/{v}/restore`. Check: update a report
  three times, list shows three versions, restore brings an old one back as the
  current definition (and itself becomes a new version).
- **[done] B — Filesystem report store.** Extract `IReportStore` (reports + versions);
  `DatabaseReportStore` (the current EF path) and `FileSystemReportStore` (one
  JSON file per report under a configurable root, `v{n}.json` snapshots),
  selected by `Storage:ReportStore` = `database` | `filesystem`. Connections and
  their secrets stay in the database. Check: with `filesystem`, the CRUD +
  versioning integration tests pass against a temp directory.
- **[done] C — Designer UX.** Restore marquee selection (regressed in Phase 2); copy /
  paste / duplicate (Ctrl+C/V/D) with an offset; alignment guides + edge snap
  while dragging; double-click to edit a label/field text in place; z-order
  (bring forward / send back). Check: manual pass in the browser.
- **[done] D — Validation surfaces.** `ReportDefinitionValidator` also checks that every
  `{ds.field}` / table / group reference names a declared data source; a
  `POST /api/reports/validate` returns the issue list; the designer shows a
  problems panel and marks offending elements. Check: a report with a dangling
  binding lists one issue and highlights the element; a clean report lists none.
- **[done] E — Ops & cleanup.**
  - Data Protection keys persisted to `DataProtection:KeyPath` when set
    (`PersistKeysToFileSystem`).
  - Built-in samples embedded in the API; `GET /api/meta/samples`; the designer's
    "Sample…" picker creates a report from one.
  - A modest concurrency check on `/api/render` (xunit): N parallel renders all
    succeed.
  - Drop QuestPDF — remove the package, `QuestPdfRenderer`, and the spike's
    QuestPDF cases; `IPdfRenderer` + `MigraDocPdfRenderer` remain. Update docs/04.
  - README + CHANGELOG.

## Phase 4 verification

Design a report, save it a few times, restore an older version. Switch the store
to `filesystem` and repeat. Introduce a broken binding and see it flagged.
Load the sample gallery. Build the container with a persisted key path and render.
