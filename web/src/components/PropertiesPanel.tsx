import { useDesigner } from "../store";
import type {
  AggregateFunction,
  AggregateScope,
  Band,
  PageSize,
  ReportElement,
  TextAlign,
} from "../types";
import { edge } from "../types";

export function PropertiesPanel() {
  const report = useDesigner((s) => s.report);
  const selectedIds = useDesigner((s) => s.selectedIds);
  const selectedBand = useDesigner((s) => s.selectedBand);
  const mutateElement = useDesigner((s) => s.mutateElement);
  const locate = useDesigner((s) => s.locate);

  if (!report) return null;

  if (selectedBand !== null && report.bands[selectedBand]) {
    return <BandProperties index={selectedBand} />;
  }

  if (selectedIds.length === 1) {
    const id = selectedIds[0];
    const el =
      report.body?.elements.find((e) => e.id === id) ??
      report.bands.flatMap((b) => b.elements).find((e) => e.id === id);
    if (el) {
      const loc = locate(id);
      const bandType =
        loc?.container === "band" ? report.bands[loc.bandIndex]?.type : undefined;
      return (
        <ElementProperties
          element={el}
          bandType={bandType}
          onPatch={(fn) => mutateElement(id, fn)}
        />
      );
    }
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

function BandProperties({ index }: { index: number }) {
  const band = useDesigner((s) => s.report!.bands[index]) as Band;
  const sources = useDesigner((s) => s.report!.dataSources);
  const patchBand = useDesigner((s) => s.patchBand);
  const removeBand = useDesigner((s) => s.removeBand);
  const moveBand = useDesigner((s) => s.moveBand);
  const bandCount = useDesigner((s) => s.report!.bands.length);
  const isGroup = band.type === "groupHeader" || band.type === "groupFooter";

  return (
    <div className="panel">
      <h2>{band.type} band</h2>
      <div className="row">
        <button className="mini" onClick={() => moveBand(index, -1)} disabled={index === 0}>↑</button>
        <button className="mini" onClick={() => moveBand(index, 1)} disabled={index === bandCount - 1}>↓</button>
        <button className="mini" onClick={() => removeBand(index)}>Delete</button>
      </div>

      <label className="field">
        <span>Height</span>
        <input
          type="number"
          value={Math.round(band.height)}
          onChange={(e) => patchBand(index, (b) => (b.height = Math.max(8, Number(e.target.value) || 8)))}
        />
      </label>

      {band.type === "detail" && (
        <label className="field">
          <span>Data source</span>
          <select
            value={band.dataSource ?? ""}
            onChange={(e) => patchBand(index, (b) => (b.dataSource = e.target.value || undefined))}
          >
            <option value="">—</option>
            {sources.map((s) => (
              <option key={s.name}>{s.name}</option>
            ))}
          </select>
        </label>
      )}

      {isGroup && (
        <>
          <label className="field">
            <span>Group by</span>
            <input
              value={band.group?.expression ?? ""}
              placeholder="{orders.customer}"
              onChange={(e) =>
                patchBand(index, (b) => {
                  b.group ??= { dataSource: sources[0]?.name ?? "", expression: "", sort: "asc" };
                  b.group.expression = e.target.value;
                })
              }
            />
          </label>
          <label className="field">
            <span>Sort</span>
            <select
              value={band.group?.sort ?? "asc"}
              onChange={(e) =>
                patchBand(index, (b) => {
                  b.group ??= { dataSource: sources[0]?.name ?? "", expression: "", sort: "asc" };
                  b.group.sort = e.target.value as "asc" | "desc";
                })
              }
            >
              <option value="asc">Ascending</option>
              <option value="desc">Descending</option>
            </select>
          </label>
        </>
      )}

      {(band.type === "groupHeader" || band.type === "reportHeader" || band.type === "pageHeader") && (
        <label className="row" style={{ marginTop: 6 }}>
          <input
            type="checkbox"
            checked={band.repeatOnEveryPage}
            onChange={(e) => patchBand(index, (b) => (b.repeatOnEveryPage = e.target.checked))}
          />
          <span>Repeat on every page</span>
        </label>
      )}
    </div>
  );
}

const AGG_FUNCS: AggregateFunction[] = ["none", "sum", "count", "average", "min", "max", "first", "last"];
const AGG_SCOPES: AggregateScope[] = ["group", "page", "report"];

function ElementProperties({
  element,
  bandType,
  onPatch,
}: {
  element: ReportElement;
  bandType?: Band["type"];
  onPatch: (fn: (el: ReportElement) => void) => void;
}) {
  const s = element.style ?? {};
  const isText = element.type === "label" || element.type === "field" || element.type === "pageInfo";
  const inFooter = bandType === "groupFooter" || bandType === "pageFooter" || bandType === "reportFooter";

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

      {inFooter && element.type === "field" && (
        <div className="grid2">
          <label className="field">
            <span>Aggregate</span>
            <select
              value={element.aggregate ?? "none"}
              onChange={(e) => onPatch((el) => (el.aggregate = e.target.value as AggregateFunction))}
            >
              {AGG_FUNCS.map((f) => (
                <option key={f} value={f}>{f}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Scope</span>
            <select
              value={element.aggregateScope ?? "group"}
              onChange={(e) => onPatch((el) => (el.aggregateScope = e.target.value as AggregateScope))}
              disabled={(element.aggregate ?? "none") === "none"}
            >
              {AGG_SCOPES.map((sc) => (
                <option key={sc} value={sc}>{sc}</option>
              ))}
            </select>
          </label>
        </div>
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
