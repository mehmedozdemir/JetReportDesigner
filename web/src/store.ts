import { create } from "zustand";
import {
  defaultElement,
  type ElementType,
  type ReportDefinition,
  type ReportElement,
  type ReportResponse,
} from "./types";

const HISTORY_LIMIT = 50;

interface DesignerState {
  report: ReportDefinition | null;
  reportId: string | null;
  concurrencyToken: string | null;
  selectedIds: string[];
  zoom: number;
  dirty: boolean;
  past: ReportDefinition[];
  future: ReportDefinition[];

  load(response: ReportResponse): void;
  markSaved(response: ReportResponse): void;

  /** Apply a mutation. `history` pushes an undo checkpoint first (default true). */
  mutate(recipe: (report: ReportDefinition) => void, history?: boolean): void;
  /** Push an undo checkpoint without mutating — call once at the start of a drag gesture. */
  checkpoint(): void;
  undo(): void;
  redo(): void;

  select(ids: string[], additive?: boolean): void;
  addElement(type: ElementType, x: number, y: number): void;
  removeSelected(): void;
  nudge(dx: number, dy: number): void;
  setZoom(zoom: number): void;
}

function clone(report: ReportDefinition): ReportDefinition {
  return structuredClone(report);
}

export const useDesigner = create<DesignerState>((set, get) => ({
  report: null,
  reportId: null,
  concurrencyToken: null,
  selectedIds: [],
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
    const previous = past[past.length - 1];
    set({
      report: previous,
      past: past.slice(0, -1),
      future: [clone(report), ...future],
      dirty: true,
    });
  },

  redo: () => {
    const { past, future, report } = get();
    if (future.length === 0 || !report) return;
    const next = future[0];
    set({
      report: next,
      future: future.slice(1),
      past: report ? [...past, clone(report)] : past,
      dirty: true,
    });
  },

  select: (ids, additive = false) =>
    set((s) => ({
      selectedIds: additive ? Array.from(new Set([...s.selectedIds, ...ids])) : ids,
    })),

  addElement: (type, x, y) => {
    const element = defaultElement(type, x, y);
    get().mutate((r) => {
      r.body?.elements.push(element);
    });
    set({ selectedIds: [element.id] });
  },

  removeSelected: () => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      if (r.body) r.body.elements = r.body.elements.filter((e) => !ids.has(e.id));
    });
    set({ selectedIds: [] });
  },

  nudge: (dx, dy) => {
    const ids = new Set(get().selectedIds);
    if (ids.size === 0) return;
    get().mutate((r) => {
      r.body?.elements.forEach((e: ReportElement) => {
        if (ids.has(e.id)) {
          e.bounds.x += dx;
          e.bounds.y += dy;
        }
      });
    });
  },

  setZoom: (zoom) => set({ zoom: Math.min(2, Math.max(0.25, zoom)) }),
}));
