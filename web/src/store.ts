import { create } from "zustand";
import {
  defaultElement,
  type Band,
  type BandType,
  type ConnectionRef,
  type DataSourceDefinition,
  type ElementType,
  type LayoutMode,
  type ReportDefinition,
  type ReportElement,
  type ReportParameter,
  type ReportResponse,
} from "./types";

const HISTORY_LIMIT = 50;

const BAND_ORDER: BandType[] = [
  "reportHeader",
  "pageHeader",
  "groupHeader",
  "detail",
  "groupFooter",
  "pageFooter",
  "reportFooter",
];

export type ElementLocation =
  | { container: "body" }
  | { container: "band"; bandIndex: number };

interface DesignerState {
  report: ReportDefinition | null;
  reportId: string | null;
  concurrencyToken: string | null;
  selectedIds: string[];
  selectedBand: number | null;
  zoom: number;
  dirty: boolean;
  past: ReportDefinition[];
  future: ReportDefinition[];

  load(response: ReportResponse): void;
  markSaved(response: ReportResponse): void;

  mutate(recipe: (report: ReportDefinition) => void, history?: boolean): void;
  checkpoint(): void;
  undo(): void;
  redo(): void;

  select(ids: string[], additive?: boolean): void;
  selectBand(index: number | null): void;

  elementsOf(location: ElementLocation): ReportElement[];
  locate(id: string): ElementLocation | null;
  mutateElement(id: string, recipe: (el: ReportElement) => void, history?: boolean): void;

  addElement(type: ElementType, x: number, y: number, location?: ElementLocation): void;
  removeSelected(): void;
  nudge(dx: number, dy: number): void;
  setZoom(zoom: number): void;

  setLayoutMode(mode: LayoutMode): void;
  addBand(type: BandType): void;
  removeBand(index: number): void;
  moveBand(index: number, direction: -1 | 1): void;
  patchBand(index: number, recipe: (band: Band) => void): void;

  setDataSource(source: DataSourceDefinition | null, connectionRef?: ConnectionRef | null): void;
  addParameter(): void;
  patchParameter(index: number, recipe: (p: ReportParameter) => void): void;
  removeParameter(index: number): void;
}

function clone(report: ReportDefinition): ReportDefinition {
  return structuredClone(report);
}

function sortBands(bands: Band[]): Band[] {
  return [...bands].sort((a, b) => BAND_ORDER.indexOf(a.type) - BAND_ORDER.indexOf(b.type));
}

export const useDesigner = create<DesignerState>((set, get) => ({
  report: null,
  reportId: null,
  concurrencyToken: null,
  selectedIds: [],
  selectedBand: null,
  zoom: 1,
  dirty: false,
  past: [],
  future: [],

  load: (response) =>
    set({
      report: response.definition,
      reportId: response.id,
      concurrencyToken: response.concurrencyToken,
      selectedIds: [],
      selectedBand: null,
      dirty: false,
      past: [],
      future: [],
    }),

  markSaved: (response) =>
    set({
      report: response.definition,
      reportId: response.id,
      concurrencyToken: response.concurrencyToken,
      dirty: false,
    }),

  mutate: (recipe, history = true) => {
    const { report, past } = get();
    if (!report) return;
    const next = clone(report);
    recipe(next);
    set({
      report: next,
      dirty: true,
      past: history ? [...past.slice(-HISTORY_LIMIT + 1), clone(report)] : past,
      future: history ? [] : get().future,
    });
  },

  checkpoint: () => {
    const { report, past } = get();
    if (!report) return;
    set({ past: [...past.slice(-HISTORY_LIMIT + 1), clone(report)], future: [] });
  },

  undo: () => {
    const { past, future, report } = get();
    if (past.length === 0 || !report) return;
    set({
      report: past[past.length - 1],
      past: past.slice(0, -1),
      future: [clone(report), ...future],
      dirty: true,
    });
  },

  redo: () => {
    const { past, future, report } = get();
    if (future.length === 0 || !report) return;
    set({
      report: future[0],
      future: future.slice(1),
      past: [...past, clone(report)],
      dirty: true,
    });
  },

  select: (ids, additive = false) =>
    set((s) => ({
      selectedIds: additive ? Array.from(new Set([...s.selectedIds, ...ids])) : ids,
      selectedBand: ids.length ? null : s.selectedBand,
    })),

  selectBand: (index) => set({ selectedBand: index, selectedIds: [] }),

  elementsOf: (location) => {
    const report = get().report;
    if (!report) return [];
    return location.container === "body"
      ? report.body?.elements ?? []
      : report.bands[location.bandIndex]?.elements ?? [];
  },

  locate: (id) => {
    const report = get().report;
    if (!report) return null;
    if (report.body?.elements.some((e) => e.id === id)) return { container: "body" };
    const bandIndex = report.bands.findIndex((band: Band) => band.elements.some((e) => e.id === id));
    return bandIndex >= 0 ? { container: "band", bandIndex } : null;
  },

  mutateElement: (id, recipe, history = true) => {
    get().mutate((r) => {
      const inBody = r.body?.elements.find((e) => e.id === id);
      if (inBody) {
        recipe(inBody);
        return;
      }
      for (const band of r.bands as Band[]) {
        const el = band.elements.find((e) => e.id === id);
        if (el) {
          recipe(el);
          return;
        }
      }
    }, history);
  },

  addElement: (type, x, y, location) => {
    const report = get().report;
    if (!report) return;
    const target: ElementLocation =
      location ??
      (report.layoutMode === "free"
        ? { container: "body" }
        : { container: "band", bandIndex: Math.max(0, report.bands.findIndex((b: Band) => b.type === "detail")) });

    const element = defaultElement(type, x, y);
    get().mutate((r) => {
      if (target.container === "body") r.body?.elements.push(element);
      else r.bands[target.bandIndex]?.elements.push(element);
    });
    set({ selectedIds: [element.id], selectedBand: null });
  },

  removeSelected: () => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      if (r.body) r.body.elements = r.body.elements.filter((e) => !ids.has(e.id));
      (r.bands as Band[]).forEach((band) => {
        band.elements = band.elements.filter((e) => !ids.has(e.id));
      });
    });
    set({ selectedIds: [] });
  },

  nudge: (dx, dy) => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      const move = (e: ReportElement) => {
        if (ids.has(e.id)) {
          e.bounds.x += dx;
          e.bounds.y += dy;
        }
      };
      r.body?.elements.forEach(move);
      (r.bands as Band[]).forEach((band) => band.elements.forEach(move));
    });
  },

  setZoom: (zoom) => set({ zoom: Math.min(2, Math.max(0.25, zoom)) }),

  setLayoutMode: (mode) => {
    const report = get().report;
    if (!report || report.layoutMode === mode) return;
    get().mutate((r) => {
      r.layoutMode = mode;
      if (mode === "banded") {
        r.body = null;
        if (r.bands.length === 0) {
          const source = r.dataSources[0]?.name;
          r.bands = [
            { type: "pageHeader", height: 32, visible: true, elements: [], repeatOnEveryPage: false },
            { type: "detail", height: 24, visible: true, elements: [], dataSource: source, repeatOnEveryPage: false },
            { type: "pageFooter", height: 28, visible: true, elements: [], repeatOnEveryPage: false },
          ];
        }
      } else {
        r.bands = [];
        r.body ??= { height: 1000, elements: [] };
      }
    });
    set({ selectedIds: [], selectedBand: null });
  },

  addBand: (type) => {
    get().mutate((r) => {
      if ((r.bands as Band[]).some((b) => b.type === type)) return;
      r.bands = sortBands([
        ...(r.bands as Band[]),
        {
          type,
          height: type === "detail" ? 24 : 26,
          visible: true,
          elements: [],
          repeatOnEveryPage: type === "groupHeader",
          dataSource: type === "detail" ? r.dataSources[0]?.name : undefined,
          group:
            type === "groupHeader" || type === "groupFooter"
              ? { dataSource: r.dataSources[0]?.name ?? "", expression: "", sort: "asc" }
              : undefined,
        },
      ]);
    });
  },

  removeBand: (index) => {
    get().mutate((r) => {
      r.bands = (r.bands as Band[]).filter((_, i) => i !== index);
    });
    set({ selectedBand: null });
  },

  moveBand: (index, direction) => {
    get().mutate((r) => {
      const bands = r.bands as Band[];
      const target = index + direction;
      if (target < 0 || target >= bands.length) return;
      [bands[index], bands[target]] = [bands[target], bands[index]];
    });
  },

  patchBand: (index, recipe) => {
    get().mutate((r) => {
      const band = (r.bands as Band[])[index];
      if (band) recipe(band);
    });
  },

  setDataSource: (source, connectionRef) => {
    get().mutate((r) => {
      r.dataSources = source ? [source] : [];
      r.connections = connectionRef ? [connectionRef] : [];
      // keep detail/group bands pointed at the (single) source
      const name = source?.name;
      r.bands.forEach((b) => {
        if (b.type === "detail") b.dataSource = name;
        if (b.group) b.group.dataSource = name ?? "";
      });
    });
  },

  addParameter: () => {
    get().mutate((r) => {
      const n = r.parameters.length + 1;
      r.parameters.push({ name: `param${n}`, type: "string", label: "", required: false, defaultValue: null });
    });
  },

  patchParameter: (index, recipe) => {
    get().mutate((r) => {
      const p = r.parameters[index];
      if (p) recipe(p);
    });
  },

  removeParameter: (index) => {
    get().mutate((r) => {
      r.parameters = r.parameters.filter((_, i) => i !== index);
    });
  },
}));
