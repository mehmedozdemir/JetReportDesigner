import { useDesigner } from "../store";
import type { PageSize, ReportElement, TextAlign } from "../types";
import { edge } from "../types";

export function PropertiesPanel() {
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const mutate = useDesigner((s) => s.mutate);

  if (!report) return null;

  if (selectedIds.length === 1) {
    const el = report.body?.elements.find((e) => e.id === selectedIds[0]);
    if (el) return <ElementProperties element={el} onPatch={(fn) => mutate((r) => applyToElement(r, el.id, fn))} />;
  }

  if (selectedIds.length > 1) {
    return (
      <div className="panel">
        <h2>Properties</h2>
        <p className="hint">{selectedIds.length} elements selected</p>
      </div>
    );
  }

  return <PageProperties />;
}

function applyToElement(
  report: NonNullable<ReturnType<typeof useDesigner.getState>["report"]>,
  id: string,
  fn: (el: ReportElement) => void,
) {
  const el = report.body?.elements.find((e) => e.id === id);
  if (el) fn(el);
}

function ElementProperties({
  element,
  onPatch,
}: {
  element: ReportElement;
  onPatch: (fn: (el: ReportElement) => void) => void;
}) {
  const s = element.style ?? {};
  const isText = element.type === "label" || element.type === "field" || element.type === "pageInfo";

  return (
    <div className="panel">
      <h2>{element.type}</h2>

      <div className="grid2">
        <Num label="X" value={element.bounds.x} onChange={(v) => onPatch((e) => (e.bounds.x = v))} />
        <Num label="Y" value={element.bounds.y} onChange={(v) => onPatch((e) => (e.bounds.y = v))} />
        <Num label="W" value={element.bounds.width} onChange={(v) => onPatch((e) => (e.bounds.width = v))} />
        <Num label="H" value={element.bounds.height} onChange={(v) => onPatch((e) => (e.bounds.height = v))} />
      </div>

      {element.type === "label" && (
        <Text label="Text" value={element.text ?? ""} onChange={(v) => onPatch((e) => (e.text = v))} />
      )}
      {(element.type === "field" || element.type === "pageInfo") && (
        <>
          <Text label="Value / binding" value={element.value ?? ""} onChange={(v) => onPatch((e) => (e.value = v))} />
          <Text label="Format" value={element.format ?? ""} placeholder="n2, dd.MM.yyyy, c" onChange={(v) => onPatch((e) => (e.format = v || null))} />
        </>
      )}
      {element.type === "image" && (
        <Text label="Source (URL / data URI)" value={element.image?.source ?? ""} onChange={(v) => onPatch((e) => (e.image = { source: v, fit: e.image?.fit ?? "contain" }))} />
      )}

      {isText && (
        <>
          <div className="grid2">
            <Text label="Font" value={s.font?.family ?? ""} placeholder="Helvetica" onChange={(v) => onPatch((e) => setFont(e, "family", v || null))} />
            <Num label="Size (pt)" value={s.font?.size ?? 10} onChange={(v) => onPatch((e) => setFont(e, "size", v))} />
          </div>
          <div className="row">
            <Toggle label="B" active={!!s.font?.bold} onClick={() => onPatch((e) => setFont(e, "bold", !e.style?.font?.bold))} />
            <Toggle label="I" active={!!s.font?.italic} onClick={() => onPatch((e) => setFont(e, "italic", !e.style?.font?.italic))} />
            <AlignPicker value={(s.align as TextAlign) ?? "left"} onChange={(v) => onPatch((e) => setStyle(e, "align", v))} />
          </div>
        </>
      )}

      <div className="grid2">
        <Color label="Color" value={s.color ?? "#111827"} onChange={(v) => onPatch((e) => setStyle(e, "color", v))} />
        <Color label="Background" value={s.background ?? "#ffffff"} onChange={(v) => onPatch((e) => setStyle(e, "background", v))} allowClear cleared={!s.background} onClear={() => onPatch((e) => setStyle(e, "background", null))} />
      </div>

      <div className="grid2">
        <Num
          label="Border"
          value={s.border ? Math.max(s.border.top, s.border.right, s.border.bottom, s.border.left) : 0}
          onChange={(v) => onPatch((e) => setStyle(e, "border", v > 0 ? edge(v, e.style?.border?.color ?? "#111827") : null))}
        />
        <Color label="Border color" value={s.border?.color ?? "#111827"} onChange={(v) => onPatch((e) => { if (e.style?.border) e.style.border.color = v; })} />
      </div>
    </div>
  );
}

function PageProperties() {
  const report = useDesigner((s) => s.report)!;
  const mutate = useDesigner((s) => s.mutate);
  const p = report.page;
  const set = (fn: (page: typeof p) => void) => mutate((r) => fn(r.page));

  return (
    <div className="panel">
      <h2>Page</h2>
      <label className="field">
        <span>Size</span>
        <select value={p.size} onChange={(e) => set((page) => (page.size = e.target.value as PageSize))}>
          {["A4", "A5", "Letter", "Legal"].map((v) => (
            <option key={v}>{v}</option>
          ))}
        </select>
      </label>
      <label className="field">
        <span>Orientation</span>
        <select value={p.orientation} onChange={(e) => set((page) => (page.orientation = e.target.value as "portrait" | "landscape"))}>
          <option value="portrait">Portrait</option>
          <option value="landscape">Landscape</option>
        </select>
      </label>
      <div className="grid2">
        <Num label="Margin T" value={p.margins.top} onChange={(v) => set((page) => (page.margins.top = v))} />
        <Num label="Margin R" value={p.margins.right} onChange={(v) => set((page) => (page.margins.right = v))} />
        <Num label="Margin B" value={p.margins.bottom} onChange={(v) => set((page) => (page.margins.bottom = v))} />
        <Num label="Margin L" value={p.margins.left} onChange={(v) => set((page) => (page.margins.left = v))} />
      </div>
      <p className="hint">Select an element to edit its properties.</p>
    </div>
  );
}

// ---- small inputs ----

function Num({ label, value, onChange }: { label: string; value: number; onChange: (v: number) => void }) {
  return (
    <label className="field">
      <span>{label}</span>
      <input type="number" value={Math.round(value)} onChange={(e) => onChange(Number(e.target.value) || 0)} />
    </label>
  );
}

function Text({
  label,
  value,
  placeholder,
  onChange,
}: {
  label: string;
  value: string;
  placeholder?: string;
  onChange: (v: string) => void;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <input value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
    </label>
  );
}

function Color({
  label,
  value,
  onChange,
  allowClear,
  cleared,
  onClear,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  allowClear?: boolean;
  cleared?: boolean;
  onClear?: () => void;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      <span className="row">
        <input type="color" value={cleared ? "#ffffff" : value} onChange={(e) => onChange(e.target.value)} />
        {allowClear && (
          <button className="mini" onClick={onClear} title="No fill" type="button">
            {cleared ? "none" : "×"}
          </button>
        )}
      </span>
    </label>
  );
}

function Toggle({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button type="button" className={`mini ${active ? "on" : ""}`} onClick={onClick}>
      {label}
    </button>
  );
}

function AlignPicker({ value, onChange }: { value: TextAlign; onChange: (v: TextAlign) => void }) {
  return (
    <span className="row">
      {(["left", "center", "right"] as TextAlign[]).map((a) => (
        <button key={a} type="button" className={`mini ${value === a ? "on" : ""}`} onClick={() => onChange(a)}>
          {a[0].toUpperCase()}
        </button>
      ))}
    </span>
  );
}

// ---- style mutation helpers ----

function setStyle<K extends keyof NonNullable<ReportElement["style"]>>(
  el: ReportElement,
  key: K,
  value: NonNullable<ReportElement["style"]>[K],
) {
  el.style = { ...(el.style ?? {}), [key]: value };
}

function setFont(el: ReportElement, key: keyof NonNullable<NonNullable<ReportElement["style"]>["font"]>, value: unknown) {
  const style = el.style ?? {};
  el.style = { ...style, font: { ...(style.font ?? {}), [key]: value } };
}
