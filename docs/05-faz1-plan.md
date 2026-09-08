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

1. **[done] Rendering: free-layout mapper.** `ReportDefinition(free) → RenderDocument`
   via `FreeLayoutBuilder`; `PageGeometry`, `EffectiveStyle`. Fonts deferred — see
   note below. Check: `FreeLayoutPipelineTests` green.
2. **[done] Data resolution.** `JsonRows` (`resultPath` + type inference),
   `ReportDataResolver`; `BindingResolver` (`{ds.field}` / `{param:*}` + format),
   `ParameterValues`. Check: `BindingTests` + `FreeLayoutPipelineTests` green.
3. **[done] API render/preview endpoints.** `POST /api/render`,
   `/api/reports/{id}/render`, `/preview`; `/api/datasources/schema` + `/preview`.
   Check: verified by hand end-to-end (`%PDF` + HTML with bound values).
4. **[done] Designer shell.** Zustand store + undo/redo history, canvas (page, zoom,
   grid, margins), toolbox, selection (click + marquee), pointer move/resize with
   grid snap, properties panel, page setup, keyboard (Del, arrows, Ctrl+Z/Y, Ctrl+S).
   Check: create/add/move/resize/delete in the browser; save round-trips.
5. **[done] Data panel + binding UI.** Paste JSON, load field schema, draggable field
   chips, drag-to-bind onto canvas, edit binding/format in the panel. Check: bound
   `{orders.customer}` renders "Acme Ltd" in preview.
6. **[done] Preview + export.** "Preview" tab (server HTML) and "Export PDF" button.
   Check: preview + `POST /api/render?format=pdf` → 200.
7. **[partial] Polish.** Undo/redo, marquee select, arrow-nudge, snap done.
   Still open: copy/paste, alignment guides, in-canvas text editing, multi-page free
   flow, image rendering in PDF.

### Font note (moved from step 1)

Embedding Liberation fonts + a cross-platform `IFontResolver` was deferred to keep
Phase 1 focused on the designer + binding slice. Current state: `WindowsCoreFontResolver`
(Windows only). PDF rendering therefore runs on the Windows dev machine and in
Rendering.Tests' Windows-guarded cases; the Linux/container path is closed out by
the font-embedding follow-up in `docs/04` before Phase 4 containerization.

## Phase 1 verification

Paste an orders JSON, design an invoice-style single page with a title, static
labels and bound fields (with number/date formats), preview as HTML, export a PDF
whose layout matches the canvas (golden-file + visual check).
