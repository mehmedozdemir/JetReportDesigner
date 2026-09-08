// Minimal mirror of the server's ReportDefinition contract. Phase 1 replaces this
// with types generated from the OpenAPI document.

export type LayoutMode = "banded" | "free";

export interface ReportSummary {
  id: string;
  name: string;
  layoutMode: LayoutMode;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ReportDefinition {
  schemaVersion: 1;
  id?: string;
  name: string;
  description?: string | null;
  layoutMode: LayoutMode;
  unit: "px";
  page: {
    size: "A4" | "A5" | "Letter" | "Legal" | "Custom";
    orientation: "portrait" | "landscape";
    margins: { top: number; right: number; bottom: number; left: number };
    columns: 1;
  };
  parameters: unknown[];
  connections: unknown[];
  dataSources: unknown[];
  styles: Record<string, unknown>;
  bands: unknown[];
  body: {
    height: number;
    elements: unknown[];
  } | null;
}

export interface ReportResponse {
  id: string;
  definition: ReportDefinition;
  createdAtUtc: string;
  updatedAtUtc: string;
  concurrencyToken: string;
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
