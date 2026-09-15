import { RotateCcw } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePrefs, type RulerUnit, type ThemePref } from "../prefs";
import { PageHeader } from "./PageHeader";
import { LANGUAGES, type LanguageCode } from "../i18n";

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="settings-row">
      <span>{label}</span>
      <span className="settings-control">{children}</span>
    </label>
  );
}

function Check({
  label,
  value,
  onChange,
}: {
  label: string;
  value: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label className="settings-row settings-check">
      <input type="checkbox" checked={value} onChange={(e) => onChange(e.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function Segmented<T extends string>({
  value,
  options,
  onChange,
}: {
  value: T;
  options: { value: T; label: string }[];
  onChange: (v: T) => void;
}) {
  return (
    <span className="segmented" role="group">
      {options.map((o) => (
        <button key={o.value} className={value === o.value ? "on" : ""} onClick={() => onChange(o.value)}>
          {o.label}
        </button>
      ))}
    </span>
  );
}

/** Personal preferences — a Start-screen page like Team/Email rather than a modal, so the
 * left nav stays put and the URL (/settings) is a real place you can link to or refresh on.
 * Available to Viewers too: everything here (theme, units, panel behaviour) is a per-person
 * preference stored locally, nothing organization- or role-scoped. */
export function SettingsPage() {
  const { t } = useTranslation();
  const p = usePrefs();
  const set = usePrefs((s) => s.set);
  const reset = usePrefs((s) => s.reset);

  return (
    <>
      <PageHeader narrow title={t("settings.title")} description={t("settings.description")} />

      <section className="start-section start-section-narrow">
        <h3>{t("settings.view")}</h3>
        <Row label={t("settings.language")}>
          <select value={p.language} onChange={(e) => set("language", e.target.value as LanguageCode)}>
            {LANGUAGES.map((l) => (
              <option key={l.code} value={l.code}>{l.label}</option>
            ))}
          </select>
        </Row>
        <Check label={t("settings.showRulers")} value={p.showRulers} onChange={(v) => set("showRulers", v)} />
        <Check label={t("settings.showGrid")} value={p.showGrid} onChange={(v) => set("showGrid", v)} />
        <Row label={t("settings.rulerUnit")}>
          <select value={p.rulerUnit} onChange={(e) => set("rulerUnit", e.target.value as RulerUnit)}>
            <option value="mm">{t("settings.mm")}</option>
            <option value="cm">{t("settings.cm")}</option>
            <option value="px">{t("settings.px")}</option>
          </select>
        </Row>
        <Row label={t("settings.theme")}>
          <Segmented<ThemePref>
            value={p.theme}
            onChange={(v) => set("theme", v)}
            options={[
              { value: "light", label: t("settings.light") },
              { value: "dark", label: t("settings.dark") },
            ]}
          />
        </Row>
      </section>

      <section className="start-section start-section-narrow">
        <h3>{t("settings.snapping")}</h3>
        <Check label={t("settings.snapToGrid")} value={p.snapToGrid} onChange={(v) => set("snapToGrid", v)} />
        <Row label={t("settings.snapIncrement")}>
          <input
            type="number"
            min={1}
            max={50}
            value={p.gridSize}
            onChange={(e) => set("gridSize", Math.max(1, Math.min(50, Number(e.target.value) || 1)))}
          />
        </Row>
        <Check label={t("settings.snapToGuides")} value={p.snapToGuides} onChange={(v) => set("snapToGuides", v)} />
      </section>

      <section className="start-section start-section-narrow">
        <h3>{t("settings.autoSave")}</h3>
        <Row label={t("settings.autoSaveLabel")}>
          <select value={String(p.autoSaveSeconds)} onChange={(e) => set("autoSaveSeconds", Number(e.target.value))}>
            <option value="0">{t("settings.off")}</option>
            <option value="30">{t("settings.every30s")}</option>
            <option value="60">{t("settings.everyMinute")}</option>
            <option value="300">{t("settings.every5m")}</option>
          </select>
        </Row>
      </section>

      <section className="start-section start-section-narrow">
        <button className="btn" onClick={reset}>
          <RotateCcw /> {t("settings.resetDefaults")}
        </button>
      </section>
    </>
  );
}
