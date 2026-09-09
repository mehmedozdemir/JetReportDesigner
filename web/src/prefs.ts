import { create } from "zustand";
import { persist } from "zustand/middleware";

export type RulerUnit = "px" | "mm" | "cm";
export type ThemePref = "light" | "dark";

export interface Prefs {
  showRulers: boolean;
  showGrid: boolean;
  rulerUnit: RulerUnit;
  snapToGrid: boolean;
  gridSize: number;
  snapToGuides: boolean;
  theme: ThemePref;
  autoSaveSeconds: number; // 0 = off
}

export const DEFAULT_PREFS: Prefs = {
  showRulers: true,
  showGrid: true,
  rulerUnit: "mm",
  snapToGrid: true,
  gridSize: 4,
  snapToGuides: true,
  theme: "light",
  autoSaveSeconds: 0,
};

interface PrefsState extends Prefs {
  set<K extends keyof Prefs>(key: K, value: Prefs[K]): void;
  reset(): void;
}

export const usePrefs = create<PrefsState>()(
  persist(
    (set) => ({
      ...DEFAULT_PREFS,
      set: (key, value) => set({ [key]: value } as unknown as Partial<PrefsState>),
      reset: () => set({ ...DEFAULT_PREFS }),
    }),
    { name: "jrd.prefs", version: 1 },
  ),
);

function applyTheme(theme: ThemePref) {
  document.documentElement.setAttribute("data-theme", theme);
}
applyTheme(usePrefs.getState().theme);
usePrefs.subscribe((s) => applyTheme(s.theme));

// ---- unit conversion (internal unit is px = 1/96 inch) ----
export function pxToUnit(px: number, unit: RulerUnit): number {
  if (unit === "mm") return (px / 96) * 25.4;
  if (unit === "cm") return (px / 96) * 2.54;
  return px;
}
export function unitLabel(unit: RulerUnit): string {
  return unit;
}
