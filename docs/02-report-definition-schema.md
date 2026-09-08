# Report Definition Schema (schemaVersion 1)

> Canonical reference for the `ReportDefinition` document.
> Machine-readable schema: `GET /api/meta/schema` (JSON Schema draft 2020-12),
> generated from `src/JetReportDesigner.Core/Schema/report-definition.schema.json`.
> C# model: `src/JetReportDesigner.Core/Model/`.
> Serialization: `ReportJson` (camelCase, string enums, nulls omitted). A report
> round-trips byte-for-byte through storage.

Breaking changes bump `schemaVersion` and add a migration path. Additive, optional
fields do not.

---

## Root object

| Field | Type | Notes |
|-------|------|-------|
| `schemaVersion` | int | Always `1` for this version. |
| `id` | uuid | Assigned by the server on create. |
| `name` | string(1..200) | Required. |
| `description` | string? | |
| `layoutMode` | `"banded"` \| `"free"` | Determines whether `bands` or `body` is used. |
| `unit` | `"px"` | Internal unit = 1/96 inch. UI converts to mm/cm/inch for display. |
| `page` | PageSetup | See below. |
| `parameters` | Parameter[] | Run-time inputs. |
| `connections` | ConnectionRef[] | References to registered DB connections. |
| `dataSources` | DataSource[] | |
| `styles` | map<string, Style> | Named, reusable styles. |
| `bands` | Band[] | Used when `layoutMode = "banded"`; must be empty otherwise. |
| `body` | Body \| null | Used when `layoutMode = "free"`; must be null otherwise. |

## PageSetup

| Field | Type | Notes |
|-------|------|-------|
| `size` | `A4` \| `A5` \| `Letter` \| `Legal` \| `Custom` | |
| `orientation` | `portrait` \| `landscape` | |
| `customWidth`, `customHeight` | number? | Required when `size = "Custom"` (px). |
| `margins` | `{ top, right, bottom, left }` | px. |
| `columns` | int | V1: `1` only. |

## Parameter

| Field | Type | Notes |
|-------|------|-------|
| `name` | identifier | `^[A-Za-z_][A-Za-z0-9_]*$`, unique. |
| `type` | `string` \| `number` \| `boolean` \| `date` \| `dateTime` | |
| `label` | string? | Shown in the run dialog. |
| `defaultValue` | scalar? | |
| `required` | bool | |
| `allowedValues` | array? | Optional fixed picker list. |

Referenced elsewhere as `{param:name}` (in REST url/query/headers, SQL parameter
values, and expressions).

## ConnectionRef

| Field | Type | Notes |
|-------|------|-------|
| `name` | string | Unique within the report; referenced by `DataSource.sql.connection`. |
| `connectionId` | uuid | Points at a registered connection (Phase 3). |
| `provider` | `sqlServer` \| `postgreSql` \| `oracle` | |

The connection string is **never** stored in the report.

## DataSource

Exactly one config block matching `kind` is populated (`none` = all null).

| Field | Type | Notes |
|-------|------|-------|
| `name` | identifier | Unique. Bindings use `{name.field}`. |
| `kind` | `none` \| `json` \| `rest` \| `sql` | |
| `json` | `{ inlineData: string, resultPath: string }` | `resultPath` JSONPath to the row array (`$` = root). |
| `rest` | `{ url, method: "GET", headers: map, query: map, resultPath }` | Phase 3. |
| `sql` | `{ connection, commandText, parameters: [{name,value}], timeoutSeconds, maxRows }` | Phase 3. |
| `fields` | `[{ name, type }]` | Discovered/declared field list for the designer tree. Not authoritative at render time. |

## Style

All properties optional so styles layer: named style, then element `style`.

```
font:     { family?, size?(pt), bold?, italic?, underline? }
color:       "#rrggbb"
background:  "#rrggbb"
align:       left | center | right | justify
vAlign:      top | middle | bottom
border:   { top, right, bottom, left, color }   (widths in px)
padding:  { top, right, bottom, left }           (px)
```

## Band  (layoutMode = "banded")

| Field | Type | Notes |
|-------|------|-------|
| `type` | `reportHeader` \| `pageHeader` \| `groupHeader` \| `detail` \| `groupFooter` \| `pageFooter` \| `reportFooter` | |
| `height` | number | Fixed (px). No auto-grow in V1. |
| `visible` | bool | |
| `dataSource` | string? | Rows that drive a `detail` band. |
| `group` | `{ dataSource, expression, sort: asc\|desc }` | Required for group bands. |
| `repeatOnEveryPage` | bool | For header bands spanning pages. |
| `elements` | Element[] | Positioned relative to the band. |

V1 limits: single grouping level; bands are fixed height; elements cannot exceed
band bounds.

## Body  (layoutMode = "free")

| Field | Type | Notes |
|-------|------|-------|
| `height` | number | Fixed canvas height (px). Overflow flows to further pages. |
| `elements` | Element[] | Absolutely positioned; no repetition. |

## Element

| Field | Type | Notes |
|-------|------|-------|
| `id` | string | Unique within its container. |
| `type` | `label` \| `field` \| `table` \| `image` \| `line` \| `rectangle` \| `pageInfo` | |
| `bounds` | `{ x, y, width, height }` | px, relative to band (banded) or page (free). |
| `styleRef` | string? | Name in `styles`, applied first. |
| `style` | Style? | Inline overrides on top of `styleRef`. |
| `visibleWhen` | string? | Boolean expression; false hides the element. |
| `text` | string? | `label`: static text. |
| `value` | string? | `field` / `pageInfo`: binding `{ds.field}` or expression. |
| `format` | string? | .NET format string (`n2`, `dd.MM.yyyy`, `c`, …). |
| `aggregate` | `none` \| `sum` \| `count` \| `average` \| `min` \| `max` \| `first` \| `last` | `field` only. |
| `aggregateScope` | `group` \| `report` \| `page` | |
| `image` | `{ source, fit: contain\|cover\|fill\|none }` | `source` = URL / base64 / `{ds.field}`. |
| `line` | `{ orientation: horizontal\|vertical }` | |
| `table` | `{ dataSource, showHeader, columns: [{ header, value, width, format?, align }] }` | |

## Binding & expression (V1)

- Binding: `{dataSource.field}` — value from the current row context.
- Parameter: `{param:name}`.
- Expression (Phase 2): `+ - * /`, string concat, `if(cond,a,b)`,
  `sum/count/avg/min/max(...)`, `format(value, fmt)`, `pageNumber()`,
  `totalPages()`, `now()`.
- Phase 1 supports bindings + `format` only.
