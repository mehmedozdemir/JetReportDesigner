// Mirrors the server's ReportDefinition contract (docs/02). Phase 2 will generate
// this from the OpenAPI document.

export type LayoutMode = "banded" | "free";
export type ElementType = "label" | "field" | "table" | "image" | "line" | "rectangle" | "pageInfo";
export type PageSize = "A4" | "A5" | "Letter" | "Legal" | "Custom";
export type Orientation = "portrait" | "landscape";
export type TextAlign = "left" | "center" | "right" | "justify";
export type VerticalAlign = "top" | "middle" | "bottom";
export type FieldType = "string" | "number" | "boolean" | "date" | "dateTime";

export interface Bounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface FontSpec {
  family?: string | null;
  size?: number | null;
  bold?: boolean | null;
  italic?: boolean | null;
  underline?: boolean | null;
}

export interface BorderSpec {
  top: number;
  right: number;
  bottom: number;
  left: number;
  color: string;
}

export interface Spacing {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface ReportStyle {
  font?: FontSpec | null;
  color?: string | null;
  background?: string | null;
  align?: TextAlign | null;
  vAlign?: VerticalAlign | null;
  border?: BorderSpec | null;
  padding?: Spacing | null;
}

export type AggregateFunction =
  | "none"
  | "sum"
  | "count"
  | "average"
  | "min"
  | "max"
  | "first"
  | "last";
export type AggregateScope = "group" | "report" | "page";

export interface TableColumn {
  header: string;
  value: string;
  width: number;
  format?: string | null;
  align: TextAlign;
}

export interface TableSpec {
  dataSource: string;
  showHeader: boolean;
  columns: TableColumn[];
}

export interface ReportElement {
  id: string;
  type: ElementType;
  bounds: Bounds;
  styleRef?: string | null;
  style?: ReportStyle | null;
  visibleWhen?: string | null;
  text?: string | null;
  value?: string | null;
  format?: string | null;
  aggregate?: AggregateFunction;
  aggregateScope?: AggregateScope;
  image?: { source: string; fit: string } | null;
  line?: { orientation: "horizontal" | "vertical" } | null;
  table?: TableSpec | null;
}

export type BandType =
  | "reportHeader"
  | "pageHeader"
  | "groupHeader"
  | "detail"
  | "groupFooter"
  | "pageFooter"
  | "reportFooter";

export interface GroupSpec {
  dataSource: string;
  expression: string;
  sort: "asc" | "desc";
}

export interface Band {
  type: BandType;
  height: number;
  visible: boolean;
  dataSource?: string;
  group?: GroupSpec;
  repeatOnEveryPage: boolean;
  elements: ReportElement[];
}

export interface DataField {
  name: string;
  type: FieldType;
}

export interface JsonSourceConfig {
  inlineData: string;
  resultPath: string;
}

export interface DataSourceDefinition {
  name: string;
  kind: "none" | "json" | "rest" | "sql";
  json?: JsonSourceConfig | null;
  rest?: unknown | null;
  sql?: unknown | null;
  fields: DataField[];
}

export interface PageSetup {
  size: PageSize;
  orientation: Orientation;
  customWidth?: number | null;
  customHeight?: number | null;
  margins: { top: number; right: number; bottom: number; left: number };
  columns: 1;
}

export interface ReportDefinition {
  schemaVersion: 1;
  id?: string;
  name: string;
  description?: string | null;
  layoutMode: LayoutMode;
  unit: "px";
  page: PageSetup;
  parameters: unknown[];
  connections: unknown[];
  dataSources: DataSourceDefinition[];
  styles: Record<string, ReportStyle>;
  bands: Band[];
  body: { height: number; elements: ReportElement[] } | null;
}

export interface ReportSummary {
  id: string;
  name: string;
  layoutMode: LayoutMode;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ReportResponse {
  id: string;
  definition: ReportDefinition;
  createdAtUtc: string;
  updatedAtUtc: string;
  concurrencyToken: string;
}

// Page dimensions in px (1/96 inch) at 96 dpi — must match Rendering/PageGeometry.
const SIZES: Record<Exclude<PageSize, "Custom">, [number, number]> = {
  A4: [794, 1123],
  A5: [559, 794],
  Letter: [816, 1056],
  Legal: [816, 1344],
};

export function pageDimensions(page: PageSetup): { width: number; height: number } {
  let w: number;
  let h: number;
  if (page.size === "Custom") {
    w = page.customWidth ?? 794;
    h = page.customHeight ?? 1123;
  } else {
    [w, h] = SIZES[page.size];
  }
  return page.orientation === "landscape" ? { width: h, height: w } : { width: w, height: h };
}

export function emptyFreeReport(name: string): ReportDefinition {
  return {
    schemaVersion: 1,
    name,
    layoutMode: "free",
    unit: "px",
    page: {
      size: "A4",
      orientation: "portrait",
      margins: { top: 40, right: 40, bottom: 40, left: 40 },
      columns: 1,
    },
    parameters: [],
    connections: [],
    dataSources: [],
    styles: {},
    bands: [],
    body: { height: 1000, elements: [] },
  };
}

let counter = 0;
export function newElementId(type: ElementType): string {
  counter += 1;
  return `${type}_${Date.now().toString(36)}_${counter}`;
}

export function defaultElement(type: ElementType, x: number, y: number): ReportElement {
  const base: ReportElement = {
    id: newElementId(type),
    type,
    bounds: { x, y, width: 160, height: 24 },
  };
  switch (type) {
    case "label":
      return { ...base, text: "Text" };
    case "field":
      return { ...base, value: "{source.field}" };
    case "table":
      return {
        ...base,
        bounds: { x, y, width: 460, height: 120 },
        table: {
          dataSource: "",
          showHeader: true,
          columns: [
            { header: "Column 1", value: "{source.field1}", width: 160, align: "left" },
            { header: "Column 2", value: "{source.field2}", width: 120, align: "right" },
          ],
        },
        style: { border: edge(1, "#cbd5e1") },
      };
    case "line":
      return { ...base, bounds: { x, y, width: 200, height: 0 }, line: { orientation: "horizontal" } };
    case "rectangle":
      return { ...base, bounds: { x, y, width: 160, height: 100 }, style: { border: edge(1) } };
    case "image":
      return { ...base, bounds: { x, y, width: 120, height: 120 }, image: { source: "", fit: "contain" } };
    case "pageInfo":
      return { ...base, value: "{param:title}" };
    default:
      return base;
  }
}

export function edge(width: number, color = "#111827"): BorderSpec {
  return { top: width, right: width, bottom: width, left: width, color };
}
