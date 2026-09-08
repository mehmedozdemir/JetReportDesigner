# Phase 1 — Free-layout designer + static JSON + PDF

> Goal: design a single-page free-layout report from pasted JSON, bind fields,
> preview it, and render a PDF that matches the canvas. First end-to-end vertical
> slice of the real product.

## Scope (in)

- **Designer (free mode)**
  - Canvas = the A4 page at 96 dpi; CSS-transform zoom (50–200%), ruler, grid, snap-to-grid.
  - Toolbox: add `label`, `field`, `image`, `line`, `rectangle`.
  - Select (single + marquee/multi), move, resize (8 handles), delete, copy/paste, z-order, arrow-key nudge.
  - Properties panel: bounds (x/y/w/h), font (family/size/bold/italic), colour, background, alignment, border, padding, format string.
  - Page setup: size (A4/A5/Letter/Legal), orientation, margins.
  - Undo/redo.
- **Data**
  - One static JSON data source per report: paste JSON, choose `resultPath`, derive field list (name + inferred type) from the first rows.
  - Data tree in the sidebar; drag a field onto the canvas → creates a bound `field` element.
- **Binding & format**
  - `{ds.field}` resolves against the first row (design-time preview) and per-value at render.
  - `{param:name}` resolves from parameter defaults.
  - .NET format strings applied to numbers/dates.
- **Rendering**
  - `ReportDefinition` (free) + resolved data → `RenderDocument` → PDF via `IPdfRenderer` (PdfSharp/MigraDoc primary).
  - `POST /api/reports/{id}/render?format=pdf|html`, `POST /api/render` (unsaved), `POST /api/reports/{id}/preview` (HTML for the designer).
  - HTML preview emitter (fast feedback; PDF is the reference output).
- **Fonts**: embed Liberation Sans/Serif/Mono in `.Rendering`; cross-platform `IFontResolver`; delete `WindowsCoreFontResolver`. (Closes the docs/04 follow-up.)

## Scope (out — later phases)

Bands/grouping/aggregates (Phase 2), REST/SQL sources (Phase 3), expressions beyond
binding + format (Phase 2), multi-page free content is supported by the model but
Phase 1 targets single-page fidelity first, auto-height, charts, images from binding.

## Canvas technology decision

**HTML overlay, not a canvas library.** Report elements are styled boxes with text,
borders and images — HTML/CSS renders them natively at full fidelity, keeps text
crisp, makes selection/handles/inline editing straightforward, and matches how
comparable web report designers (Telerik, DevExpress) work. Konva/`react-konva`
shines for freeform vector editing we do not need and makes text and DOM affordances
harder. Drag/resize via lightweight custom pointer handlers (no `react-rnd`/dnd-kit
dependency for absolute positioning). State in Zustand with an undo/redo history
stack.

## Build order (each step builds + a check)

1. **Rendering: free-layout mapper + fonts.** `ReportDefinition(free) → RenderDocument`;
   embed Liberation fonts + resolver. Check: golden-file test renders the sample
   invoice PDF; `WindowsCoreFontResolver` gone; Rendering tests green on Linux CI.
2. **Data resolution.** JSON parse + `resultPath` + field-type inference + row access;
   `{ds.field}` / `{param:*}` binder + format. Check: unit tests for binder/format/typing.
3. **API render/preview endpoints.** `/render`, `/reports/{id}/render`, `/preview`.
   Check: integration test posts a definition, gets a `%PDF` back; preview returns HTML.
4. **Designer shell.** Zustand store, canvas with page + zoom + ruler + grid, toolbox,
   selection model, properties panel, page setup. Check: add/move/resize/delete a
   label in the browser; save round-trips.
5. **Data panel + binding UI.** Paste JSON, field tree, drag-to-bind, format field in
   panel. Check: bind a field, see the first-row value on canvas.
6. **Preview + export.** "Preview" tab (HTML) and "Export PDF" button wired to the API.
   Check: design the sample invoice, export PDF, positions match the canvas within ±2px.
7. **Undo/redo, copy/paste, nudge, polish.** Check: manual pass over the interactions.

## Phase 1 verification

Paste an orders JSON, design an invoice-style single page with a title, static
labels and bound fields (with number/date formats), preview as HTML, export a PDF
whose layout matches the canvas (golden-file + visual check).
