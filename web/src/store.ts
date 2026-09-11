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
  inspectorPulse: number;
  zoom: number;
  dirty: boolean;
  /** Server timestamp of the last successful load/save — drives the "Saved Xm ago" toolbar text. */
  savedAtUtc: string | null;
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
  revealInspector(): void;

  elementsOf(location: ElementLocation): ReportElement[];
  locate(id: string): ElementLocation | null;
  mutateElement(id: string, recipe: (el: ReportElement) => void, history?: boolean): void;
  /** Apply a recipe to every currently-selected element (one history entry). */
  mutateSelected(recipe: (el: ReportElement) => void): void;

  addElement(type: ElementType, x: number, y: number, location?: ElementLocation): void;
  removeSelected(): void;
  nudge(dx: number, dy: number): void;
  setZoom(zoom: number): void;

  copySelection(): void;
  paste(): void;
  duplicateSelection(): void;
  reorderSelection(mode: "front" | "back" | "forward" | "backward"): void;

  guides: { x: number | null; y: number | null };
  setGuides(x: number | null, y: number | null): void;

  setLayoutMode(mode: LayoutMode): void;
  addBand(type: BandType): void;
  removeBand(index: number): void;
  moveBand(index: number, direction: -1 | 1): void;
  patchBand(index: number, recipe: (band: Band) => void): void;
  /** Adds a nested group level: a groupHeader + groupFooter pair one level deeper than any existing group. */
  addGroupLevel(): void;

  setDataSource(source: DataSourceDefinition | null, connectionRef?: ConnectionRef | null): void;
  addParameter(): void;
  patchParameter(index: number, recipe: (p: ReportParameter) => void): void;
  removeParameter(index: number): void;
}

function clone<T>(value: T): T {
  return structuredClone(value);
}

let clipboard: ReportElement[] = [];

function flatElements(report: ReportDefinition | null): ReportElement[] {
  if (!report) return [];
  return [...(report.body?.elements ?? []), ...report.bands.flatMap((b) => b.elements)];
}

function firstElementLocation(state: { report: ReportDefinition | null; selectedIds: string[] }): ElementLocation {
  const r = state.report;
  if (!r) return { container: "body" };
  const id = state.selectedIds[0];
  if (id) {
    if (r.body?.elements.some((e) => e.id === id)) return { container: "body" };
    const bi = r.bands.findIndex((b) => b.elements.some((e) => e.id === id));
    if (bi >= 0) return { container: "band", bandIndex: bi };
  }
  return r.layoutMode === "free"
    ? { container: "body" }
    : { container: "band", bandIndex: Math.max(0, r.bands.findIndex((b: Band) => b.type === "detail")) };
}

function containerElements(r: ReportDefinition, loc: ElementLocation): ReportElement[] | undefined {
  return loc.container === "body" ? r.body?.elements : r.bands[loc.bandIndex]?.elements;
}

function sortBands(bands: Band[]): Band[] {
  return [...bands].sort((a, b) => {
    const byType = BAND_ORDER.indexOf(a.type) - BAND_ORDER.indexOf(b.type);
    if (byType !== 0) return byType;
    // Nested groups sandwich the detail band: outer header first, inner header last,
    // then (after detail) inner footer first, outer footer last.
    if (a.type === "groupHeader") return (a.groupLevel ?? 0) - (b.groupLevel ?? 0);
    if (a.type === "groupFooter") return (b.groupLevel ?? 0) - (a.groupLevel ?? 0);
    return 0;
  });
}

export const useDesigner = create<DesignerState>((set, get) => ({
  report: null,
  reportId: null,
  concurrencyToken: null,
  selectedIds: [],
  selectedBand: null,
  inspectorPulse: 0,
  zoom: 1,
  dirty: false,
  savedAtUtc: null,
  past: [],
  future: [],
  guides: { x: null, y: null },

  load: (response) =>
    set({
      report: response.definition,
      reportId: response.id,
      concurrencyToken: response.concurrencyToken,
      selectedIds: [],
      selectedBand: null,
      dirty: false,
      savedAtUtc: response.updatedAtUtc,
      past: [],
      future: [],
    }),

  markSaved: (response) =>
    set({
      report: response.definition,
      reportId: response.id,
      concurrencyToken: response.concurrencyToken,
      dirty: false,
      savedAtUtc: response.updatedAtUtc,
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

  revealInspector: () => set((s) => ({ inspectorPulse: s.inspectorPulse + 1 })),

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

  mutateSelected: (recipe) => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      const apply = (el: ReportElement) => {
        if (ids.has(el.id)) recipe(el);
      };
      r.body?.elements.forEach(apply);
      (r.bands as Band[]).forEach((band) => band.elements.forEach(apply));
    });
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

  copySelection: () => {
    const ids = new Set(get().selectedIds);
    const all = flatElements(get().report);
    clipboard = all.filter((e) => ids.has(e.id)).map((e) => clone(e));
  },

  paste: () => {
    if (clipboard.length === 0) return;
    const loc = get().selectedBand !== null
      ? ({ container: "band", bandIndex: get().selectedBand! } as ElementLocation)
      : firstElementLocation(get());
    const fresh = clipboard.map((e) => ({
      ...clone(e),
      id: `${e.type}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 6)}`,
      bounds: { ...e.bounds, x: e.bounds.x + 12, y: e.bounds.y + 12 },
    }));
    get().mutate((r) => {
      const target = containerElements(r, loc);
      target?.push(...fresh);
    });
    set({ selectedIds: fresh.map((e) => e.id), selectedBand: null });
  },

  duplicateSelection: () => {
    get().copySelection();
    get().paste();
  },

  reorderSelection: (mode) => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      const containers: ReportElement[][] = [
        ...(r.body ? [r.body.elements] : []),
        ...r.bands.map((b) => b.elements),
      ];
      for (const list of containers) {
        const picked = list.filter((e) => ids.has(e.id));
        if (picked.length === 0) continue;
        const rest = list.filter((e) => !ids.has(e.id));
        if (mode === "front") list.splice(0, list.length, ...rest, ...picked);
        else if (mode === "back") list.splice(0, list.length, ...picked, ...rest);
        else {
          // forward / backward: shift each picked element one slot
          const step = mode === "forward" ? 1 : -1;
          const order = mode === "forward" ? [...picked].reverse() : picked;
          for (const el of order) {
            const i = list.indexOf(el);
            const j = i + step;
            if (j >= 0 && j < list.length && !ids.has(list[j].id)) {
              [list[i], list[j]] = [list[j], list[i]];
            }
          }
        }
      }
    });
  },

  setGuides: (x, y) => set({ guides: { x, y } }),

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

  addGroupLevel: () => {
    get().mutate((r) => {
      const bands = r.bands as Band[];
      const existingLevels = bands
        .filter((b) => b.type === "groupHeader" || b.type === "groupFooter")
        .map((b) => b.groupLevel ?? 0);
      const nextLevel = existingLevels.length ? Math.max(...existingLevels) + 1 : 0;
      const source = r.dataSources[0]?.name ?? "";
      r.bands = sortBands([
        ...bands,
        {
          type: "groupHeader", height: 26, visible: true, elements: [],
          repeatOnEveryPage: true, groupLevel: nextLevel,
          group: { dataSource: source, expression: "", sort: "asc" },
        },
        {
          type: "groupFooter", height: 26, visible: true, elements: [],
          repeatOnEveryPage: false, groupLevel: nextLevel,
          group: { dataSource: source, expression: "", sort: "asc" },
        },
      ]);
    });
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
