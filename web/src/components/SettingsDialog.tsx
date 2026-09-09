import { useEffect } from "react";
import { RotateCcw, X } from "lucide-react";
import { usePrefs, type RulerUnit, type ThemePref } from "../prefs";

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

export function SettingsDialog({ onClose }: { onClose: () => void }) {
  const p = usePrefs();
  const set = usePrefs((s) => s.set);
  const reset = usePrefs((s) => s.reset);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="modal-backdrop" onMouseDown={onClose}>
      <div
        className="modal settings-dialog"
        role="dialog"
        aria-modal="true"
        aria-label="Settings"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <header>
          <h2>Settings</h2>
          <button className="mini ghost" onClick={onClose} aria-label="Close">
            <X />
          </button>
        </header>

        <div className="settings-body">
          <section className="settings-section">
            <h3>View</h3>
            <Check label="Show rulers" value={p.showRulers} onChange={(v) => set("showRulers", v)} />
            <Check label="Show grid dots" value={p.showGrid} onChange={(v) => set("showGrid", v)} />
            <Row label="Ruler unit">
              <select value={p.rulerUnit} onChange={(e) => set("rulerUnit", e.target.value as RulerUnit)}>
                <option value="mm">Millimetres (mm)</option>
                <option value="cm">Centimetres (cm)</option>
                <option value="px">Pixels (px)</option>
              </select>
            </Row>
            <Row label="Theme">
              <Segmented<ThemePref>
                value={p.theme}
                onChange={(v) => set("theme", v)}
                options={[
                  { value: "light", label: "Light" },
                  { value: "dark", label: "Dark" },
                ]}
              />
            </Row>
          </section>

          <section className="settings-section">
            <h3>Snapping</h3>
            <Check label="Snap to grid" value={p.snapToGrid} onChange={(v) => set("snapToGrid", v)} />
            <Row label="Snap increment (px)">
              <input
                type="number"
                min={1}
                max={50}
                value={p.gridSize}
                onChange={(e) => set("gridSize", Math.max(1, Math.min(50, Number(e.target.value) || 1)))}
              />
            </Row>
            <Check
              label="Snap to alignment guides"
              value={p.snapToGuides}
              onChange={(v) => set("snapToGuides", v)}
            />
          </section>

          <section className="settings-section">
            <h3>Auto-save</h3>
            <Row label="Save edited reports">
              <select
                value={String(p.autoSaveSeconds)}
                onChange={(e) => set("autoSaveSeconds", Number(e.target.value))}
              >
                <option value="0">Off</option>
                <option value="30">Every 30 seconds</option>
                <option value="60">Every minute</option>
                <option value="300">Every 5 minutes</option>
              </select>
            </Row>
          </section>
        </div>

        <footer>
          <button className="btn" onClick={reset}>
            <RotateCcw /> Reset to defaults
          </button>
          <span style={{ marginLeft: "auto" }} />
          <button className="btn primary" onClick={onClose}>
            Done
          </button>
        </footer>
      </div>
    </div>
  );
}
