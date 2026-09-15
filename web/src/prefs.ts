import { create } from "zustand";
import { persist } from "zustand/middleware";
import { detectLanguage, i18next, initI18n, type LanguageCode } from "./i18n";

export type RulerUnit = "px" | "mm" | "cm";
export type ThemePref = "light" | "dark";
export type FolderViewMode = "grid" | "detail";

export interface Prefs {
  showRulers: boolean;
  showGrid: boolean;
  rulerUnit: RulerUnit;
  snapToGrid: boolean;
  gridSize: number;
  snapToGuides: boolean;
  theme: ThemePref;
  autoSaveSeconds: number; // 0 = off
  leftPanelWidth: number;
  rightPanelWidth: number;
  leftPanelCollapsed: boolean;
  rightPanelCollapsed: boolean;
  folderViewMode: FolderViewMode;
  language: LanguageCode;
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
  leftPanelWidth: 272,
  rightPanelWidth: 300,
  leftPanelCollapsed: false,
  rightPanelCollapsed: false,
  folderViewMode: "grid",
  // Defaults to the browser's language, so a Turkish-speaking user doesn't have to find the
  // setting first. Explicitly choosing one in Settings persists and wins from then on.
  language: detectLanguage(),
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

// i18next holds the live language; this store is what persists it. Initialised from the stored
// value and kept in step with it, so a language change is a single `set("language", …)`.
initI18n(usePrefs.getState().language);
function applyLanguage(language: LanguageCode) {
  if (i18next.language !== language) void i18next.changeLanguage(language);
  document.documentElement.setAttribute("lang", language);
}
applyLanguage(usePrefs.getState().language);
usePrefs.subscribe((s) => applyLanguage(s.language));

// ---- unit conversion (internal unit is px = 1/96 inch) ----
export function pxToUnit(px: number, unit: RulerUnit): number {
  if (unit === "mm") return (px / 96) * 25.4;
  if (unit === "cm") return (px / 96) * 2.54;
  return px;
}
export function unitLabel(unit: RulerUnit): string {
  return unit;
}
