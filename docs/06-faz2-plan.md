# Phase 2 — Banded reports

> Goal: a multi-page, grouped list report driven by a data source — detail band
> repeats per row, one grouping level with header/footer, sum/count/avg/min/max
> aggregates at group/page/report scope, page header/footer with page numbers,
> and a table element. Rendered to PDF + HTML with correct pagination.

## Scope (in)

- **Band model** (already in `ReportDefinition.bands`): `reportHeader`,
  `pageHeader`, `groupHeader`, `detail`, `groupFooter`, `pageFooter`,
  `reportFooter`. Fixed heights (no auto-grow in V1).
- **Detail iteration**: the `detail` band renders once per row of its data source.
- **Single-level grouping**: one `groupHeader` + `groupFooter` pair keyed by a
  `{ds.field}` group expression with `asc` / `desc` sort. Nested groups → later.
- **Aggregates**: an element with `aggregate` ∈ {sum,count,average,min,max,first,last}
  over `aggregateScope` ∈ {group, page, report}, bound to `{ds.field}`.
- **Page furniture**: `pageHeader` / `pageFooter` on every page; `pageInfo`
  element resolving `pageNumber()`, `totalPages()`, `now()` tokens.
- **Table element** (`ElementType.Table`): fixed columns, optional header row,
  one output row per data-source row; usable in free and banded layouts.
- **Pagination**: fill the page's usable band area top→bottom; break before a band
  that doesn't fit; re-emit `pageHeader` (and `groupHeader` when
  `repeatOnEveryPage`) after a break; `reportFooter` breaks to its own page if
  needed. Two-pass so `totalPages()` is known.
- **Rendering**: `BandedLayoutBuilder` → `RenderDocument`; existing PDF/HTML
  emitters unchanged.
- **Designer**: layout-mode switch (free ↔ banded), band strip editor
  (add/remove/reorder/resize, type + data source + group config), element
  aggregate config, columns editor for the table element.

## Scope (out — later)

Nested grouping, `can-grow` / auto-height, push-up / collapse, multi-column page
layout, cross-tab / matrix, sub-reports, charts, per-report culture, full
expression language beyond the Phase 2 subset below.

## Expression subset (Phase 2)

`BindingResolver` gains, alongside `{ds.field}` / `{param:name}`:
`pageNumber()`, `totalPages()`, `now()` — resolved from a render context.
A small arithmetic / `if(cond,a,b)` / string-concat evaluator is a **separate
slice (D)**, added only if slices A–C leave room; the verification report does
not depend on it.

## Build order (each slice builds + a check, then a commit)

- **A — Banded rendering engine.** `BandedLayoutBuilder`, `AggregateComputer`,
  page-info token resolution, two-pass pagination; `ReportRenderService` routes
  banded reports here (drop the `501`). Check: unit tests for a grouped 40-row
  JSON report — expected page count, group-footer subtotals, report grand total,
  page numbers.
- **B — Banded designer.** Store: layout-mode switch + band CRUD + element↔band
  addressing. Canvas renders band strips with per-band resize and an "add band"
  menu; band inspector (type, data source, group expr + sort); element inspector
  gains aggregate + scope when the element sits in a footer band. Check: build a
  grouped report in the browser, preview paginates.
- **C — Table element.** Renderer emits a table (header + one row per data row) in
  both layouts; designer columns editor (header, binding, width, format, align).
  Check: a table bound to the JSON source renders all rows in preview + PDF.
- **D — Expression evaluator (optional).** `if`, `+ - * / %`, unary minus, string
  concat, parens, literals; functions `format`, `pageNumber`, `totalPages`, `now`.
  Check: unit tests.

## Phase 2 verification

Paste a ~40-row orders JSON, design a banded report grouped by customer with a
per-group subtotal, a grand total in the report footer, and "Page N / M" in the
page footer. Preview and PDF are multi-page, groups break correctly, totals are
right (golden-file + pagination unit tests).
